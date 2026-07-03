using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NowUI
{
    public enum NowWorldFacingMode
    {
        None,
        FaceCamera,
        FaceCameraYOnly
    }

    public enum NowWorldDepthMode
    {
        AlwaysVisible,
        SceneOccluded
    }

    public enum NowWorldGlassDepthMode
    {
        Disabled,
        ClipForeground
    }

    [Flags]
    public enum NowWorldAutoSizeAxes
    {
        None = 0,
        Width = 1,
        Height = 2,
        Both = Width | Height
    }

    public readonly struct NowWorldVertex
    {
        public readonly int index;
        public readonly Vector2 uiPosition;
        public readonly Vector2 normalized;
        public readonly Vector3 localPosition;

        public NowWorldVertex(int index, Vector2 uiPosition, Vector2 normalized, Vector3 localPosition)
        {
            this.index = index;
            this.uiPosition = uiPosition;
            this.normalized = normalized;
            this.localPosition = localPosition;
        }
    }

    public abstract class NowWorldDeformer : MonoBehaviour
    {
        public virtual Vector3 Deform(in NowWorldVertex vertex)
        {
            return vertex.localPosition;
        }
    }

    [AddComponentMenu("NowUI/Now World Graphic")]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class NowWorldGraphic : MonoBehaviour, INowPopupFitProvider
    {
        static readonly int _zTestId = Shader.PropertyToID("_ZTest");
        static readonly int _nowMaterialGlassModeId = Shader.PropertyToID("_NowMaterialGlassMode");
        static readonly int _nowBackdropTexId = Shader.PropertyToID("_NowMaterialBackdropTex");
        static readonly int _nowBackdropUvTransformId = Shader.PropertyToID("_NowMaterialBackdropUVTransform");
        static readonly int _nowGlassSharpBackdropTexId = Shader.PropertyToID("_NowMaterialGlassSharpBackdropTex");
        static readonly int _nowGlassUseBackdropId = Shader.PropertyToID("_NowMaterialGlassUseBackdrop");
        static readonly int _nowGlassUseSceneDepthId = Shader.PropertyToID("_NowMaterialGlassUseSceneDepth");
        static readonly int _nowGlassDepthEpsilonId = Shader.PropertyToID("_NowMaterialGlassDepthEpsilon");
        const float InputOcclusionEpsilon = 0.0001f;
        const float GlassDepthEpsilon = 0.02f;
        const float GlassSceneDepthBlurThreshold = 0.25f;

        static int _nextScopeId;
        static int _inputResolverVersion;
        static readonly List<NowWorldGraphic> _instances = new List<NowWorldGraphic>(16);
        static readonly RaycastHit[] _sceneOcclusionHits = new RaycastHit[16];
        static readonly Plane[] _frustumPlanes = new Plane[6];
        static Camera _frustumPlanesCamera;
        static int _frustumPlanesFrame = -1;
        static InputRayResolution _inputRayResolution;
        static Camera _backdropSortCamera;

        [SerializeField] Camera _targetCamera;
        [SerializeField] NowWorldFacingMode _facingMode = NowWorldFacingMode.FaceCamera;
        [SerializeField] NowWorldDepthMode _depthMode = NowWorldDepthMode.AlwaysVisible;
        [SerializeField] NowWorldAutoSizeAxes _layoutAutoSizeAxes;
        [SerializeField] bool _frustumCullRebuilds;
        [SerializeField] Vector2 _size = new Vector2(220f, 72f);
        [SerializeField] Vector2 _pivot = new Vector2(0.5f, 0.5f);
        [SerializeField, Min(0.0001f)] float _pixelsPerUnit = 100f;
        [SerializeField, Min(0.0001f)] float _antiAliasSmoothing = 1f;
        [SerializeField] bool _rebuildEveryFrame;
        [SerializeField] bool _autoRebuildOnInteraction = true;
        [SerializeField, Tooltip("Withhold pointer input while the pointer is over raycastable UGUI, because screen-space UI draws in front of world-space NowUI.")]
        bool _blockedWhenPointerOverUGUI = true;
        [SerializeField] bool _acceptNavigation;
        [SerializeField] NowWorldGlassBackdropMode _glassBackdropMode = NowWorldGlassBackdropMode.CameraAndWorld;
        [SerializeField] NowGlassBlurQuality _glassBlurQuality = NowGlassBlurQuality.Auto;
        [SerializeField] NowWorldDeformer _deformer;

        [NonSerialized] MeshFilter _meshFilter;
        [NonSerialized] MeshRenderer _meshRenderer;
        [NonSerialized] NowDrawList _drawList;
        [NonSerialized] NowWorldInputProvider _inputProvider;
        [NonSerialized] Texture _glassBackdropTexture;
        [NonSerialized] Texture _glassSharpBackdropTexture;
        [NonSerialized] readonly List<Vector3> _vertices = new List<Vector3>(256);
        [NonSerialized] readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>(8);
        [NonSerialized] readonly List<Material> _sharedMaterials = new List<Material>(8);
        [NonSerialized] bool _sharedMaterialsAssigned;
        [NonSerialized] Camera _fallbackCamera;
        [NonSerialized] bool _dirty = true;
        [NonSerialized] NowInteractionRepaintTracker _repaintTracker;
        [NonSerialized] bool _hasGlassBatches;
        [NonSerialized] float _maxGlassBlurRadius;
        [NonSerialized] NowGlassBlurQuality _maxGlassBlurQuality = NowGlassBlurQuality.Balanced;
        [NonSerialized] int _scopeId;
#if UNITY_EDITOR
        static bool _editorCallbacksRegistered;

        [NonSerialized] bool _editorRebuildQueued;
#endif

        struct InputRayResolution
        {
            public bool valid;
            public int frame;
            public int version;
            public Camera camera;
            public Vector2 screenPosition;
            public Ray ray;
            public NowWorldGraphic owner;
            public Vector2 ownerSurfacePosition;
            public float ownerDistance;
            public float sceneBlockDistance;
        }

        public event Action<NowWorldGraphic, NowRect> rebuildNowUI;

        public Vector2 size
        {
            get => _size;
            set
            {
                var sanitized = SanitizeSize(value);

                if ((_size - sanitized).sqrMagnitude <= 0.0001f)
                    return;

                _size = sanitized;
                MarkDirty();
                _inputProvider?.ResetPosition();
                InvalidateInputResolution();
            }
        }

        public float pixelsPerUnit
        {
            get => _pixelsPerUnit;
            set
            {
                float sanitized = SanitizePixelsPerUnit(value);

                if (Mathf.Approximately(_pixelsPerUnit, sanitized))
                    return;

                _pixelsPerUnit = sanitized;
                MarkDirty();
                _inputProvider?.ResetPosition();
                InvalidateInputResolution();
            }
        }

        public Vector2 pivot
        {
            get => _pivot;
            set
            {
                if ((_pivot - value).sqrMagnitude <= 0.0001f)
                    return;

                _pivot = value;
                MarkDirty();
                _inputProvider?.ResetPosition();
                InvalidateInputResolution();
            }
        }

        public float antiAliasSmoothing
        {
            get => _antiAliasSmoothing;
            set
            {
                float sanitized = SanitizeAntiAliasSmoothing(value);

                if (Mathf.Approximately(_antiAliasSmoothing, sanitized))
                    return;

                _antiAliasSmoothing = sanitized;
                MarkDirty();
            }
        }

        public NowWorldFacingMode facingMode
        {
            get => _facingMode;
            set => _facingMode = value;
        }

        public Camera targetCamera
        {
            get => _targetCamera;
            set
            {
                _targetCamera = value;
                _inputProvider?.ResetPosition();
                InvalidateInputResolution();
            }
        }

        public NowWorldDepthMode depthMode
        {
            get => _depthMode;
            set
            {
                if (_depthMode == value)
                    return;

                _depthMode = value;
                MarkDirty();
                InvalidateInputResolution();
            }
        }

        public NowWorldAutoSizeAxes layoutAutoSizeAxes
        {
            get => _layoutAutoSizeAxes;
            set
            {
                if (_layoutAutoSizeAxes == value)
                    return;

                _layoutAutoSizeAxes = value;
                MarkDirty();
                _inputProvider?.ResetPosition();
                InvalidateInputResolution();
            }
        }

        public bool frustumCullRebuilds
        {
            get => _frustumCullRebuilds;
            set => _frustumCullRebuilds = value;
        }

        public bool rebuildEveryFrame
        {
            get => _rebuildEveryFrame;
            set => _rebuildEveryFrame = value;
        }

        public bool autoRebuildOnInteraction
        {
            get => _autoRebuildOnInteraction;
            set => _autoRebuildOnInteraction = value;
        }

        public bool blockedWhenPointerOverUGUI
        {
            get => _blockedWhenPointerOverUGUI;
            set
            {
                if (_blockedWhenPointerOverUGUI == value)
                    return;

                _blockedWhenPointerOverUGUI = value;
                _inputProvider?.ResetPosition();
            }
        }

        public bool acceptNavigation
        {
            get => _acceptNavigation;
            set
            {
                if (_acceptNavigation == value)
                    return;

                _acceptNavigation = value;
                _inputProvider?.ResetPosition();
            }
        }

        public NowWorldGlassBackdropMode glassBackdropMode
        {
            get => NowWorldGlassBackdrop.NormalizeMode(_glassBackdropMode);
            set
            {
                value = NowWorldGlassBackdrop.NormalizeMode(value);

                if (glassBackdropMode == value)
                    return;

                _glassBackdropMode = value;
                ApplyRendererState();

                if (_glassBackdropMode == NowWorldGlassBackdropMode.TintOnly)
                    ApplyGlassBackdropTexture(null);
            }
        }

        public NowGlassBlurQuality glassBlurQuality
        {
            get => _glassBlurQuality;
            set
            {
                if (_glassBlurQuality == value)
                    return;

                _glassBlurQuality = value;
                MarkDirty();
            }
        }

        public NowWorldDeformer deformer
        {
            get => _deformer;
            set
            {
                if (_deformer == value)
                    return;

                _deformer = value;
                MarkDirty();
            }
        }

        protected virtual bool useLayoutMeasurePass => true;

        struct FrameContent : INowFrameContent
        {
            readonly NowWorldGraphic _owner;

            public FrameContent(NowWorldGraphic owner)
            {
                _owner = owner;
            }

            public void Draw(NowRect rect)
            {
                _owner.DrawNowUI(rect);
            }
        }

        public Mesh mesh => _drawList?.mesh;

        public int batchCount => _drawList?.batchCount ?? 0;

        public bool hasGeometry => _drawList is { hasGeometry: true };

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void RebuildNowUI()
        {
            using var profile = NowProfiler.WorldRebuild.Auto();

            EnsureRuntimeObjects();
            ApplyFacing();

            var currentSize = SanitizeSize(_size);
            _size = currentSize;
            _pixelsPerUnit = SanitizePixelsPerUnit(_pixelsPerUnit);
            NowDrawScope scope = default;
            var frame = NowFrame.Begin(ResolveScreenPixelsPerUIUnit(currentSize), trackRepaint: true);
            _repaintTracker.SetWantsRepaint(false);

            try
            {
                if (_layoutAutoSizeAxes != NowWorldAutoSizeAxes.None)
                {
                    currentSize = ResolveLayoutAutoSize(currentSize);
                    Now.SetUIScale(ResolveScreenPixelsPerUIUnit(currentSize));
                }

                var surface = new NowInputSurface(currentSize);
                scope = _drawList.Begin(currentSize, _glassBlurQuality);

                using (NowOverlay.Host(this))
                using (NowPopupPlacement.FitProvider(this))
                using (NowInput.Begin(GetInputProvider(), surface))
                using (NowControls.IdScope(GetScopeId()))
                {
                    _repaintTracker.StoreFrameInput(NowInput.current, currentSize);

                    var content = new FrameContent(this);
                    NowFrame.DrawContent(
                        ref content,
                        new NowRect(0f, 0f, currentSize.x, currentSize.y),
                        useLayoutMeasurePass,
                        trackContent: false);
                }

                _repaintTracker.SetWantsRepaint(frame.EndRepaintTracking());
                scope.Dispose();
                ApplyWorldTransform();
                ApplyRendererState();
                _dirty = false;
            }
            catch (Exception ex)
            {
                scope.Cancel();
                _drawList.Clear();
                ApplyRendererState();
                Debug.LogException(ex, this);
            }
            finally
            {
                frame.Dispose();
            }
        }

        public Vector3 UIToLocal(Vector2 uiPosition)
        {
            var currentSize = SanitizeSize(_size);
            float ppu = SanitizePixelsPerUnit(_pixelsPerUnit);

            return UIToLocal(uiPosition, currentSize, ppu);
        }

        public Vector2 LocalToUI(Vector3 localPosition)
        {
            var currentSize = SanitizeSize(_size);
            float ppu = SanitizePixelsPerUnit(_pixelsPerUnit);

            return new Vector2(
                localPosition.x * ppu + currentSize.x * _pivot.x,
                currentSize.y * (1f - _pivot.y) - localPosition.y * ppu);
        }

        NowRect INowPopupFitProvider.FitPopupRectToView(NowRect rect)
        {
            return FitPopupRectToCameraView(rect);
        }

        NowRect INowPopupFitProvider.ClampPopupRectToView(NowRect rect)
        {
            return ClampPopupRectToCameraView(rect);
        }

        /// <summary>
        /// Shrinks a popup until its screen-space projection fits the camera's
        /// pixel rect, then moves it into view. Projection-based, so it clamps
        /// correctly on tilted surfaces where a plane-projected bounding box
        /// would explode toward the plane's horizon.
        /// </summary>
        NowRect ClampPopupRectToCameraView(NowRect rect)
        {
            var cmr = ResolveCamera();

            if (!cmr || rect.isEmpty)
                return rect;

            Rect pixels = cmr.pixelRect;

            if (pixels.height <= 1f)
                return FitPopupRectToCameraView(rect);

            const float margin = 8f;
            const float minPopupHeight = 48f;
            float maxPixels = Mathf.Max(32f, pixels.height - margin * 2f);

            for (int i = 0; i < 4; ++i)
            {
                if (!TryProjectPopupScreenBounds(cmr, rect, out var screenBounds))
                    break;

                if (screenBounds.height <= maxPixels || screenBounds.height <= 1f)
                    break;

                float ratio = maxPixels / screenBounds.height;
                float newHeight = Mathf.Max(minPopupHeight, rect.height * ratio);

                if (newHeight >= rect.height - 0.5f)
                    break;

                rect = new NowRect(rect.x, rect.y, rect.width, newHeight);
            }

            return FitPopupRectToCameraView(rect);
        }

        NowRect FitPopupRectToCameraView(NowRect rect)
        {
            var cmr = ResolveCamera();

            if (!cmr || rect.isEmpty)
                return rect;

            Rect bounds = cmr.pixelRect;

            if (bounds.width <= 1f || bounds.height <= 1f)
                return rect;

            var fitted = rect;

            for (int i = 0; i < 6; ++i)
            {
                if (!TryProjectPopupScreenBounds(cmr, fitted, out var screenBounds))
                    return fitted;

                Vector2 screenDelta = CalculateScreenFitDelta(screenBounds, bounds);

                if (screenDelta.sqrMagnitude <= 0.01f)
                    return fitted;

                if (!TryScreenDeltaToUIDelta(cmr, fitted.center, screenDelta, out var uiDelta))
                    return fitted;

                if (!IsFinite(uiDelta) || uiDelta.sqrMagnitude <= 0.0001f)
                    return fitted;

                fitted = fitted.Offset(uiDelta);
            }

            return fitted;
        }

        bool TryProjectPopupScreenBounds(Camera cmr, NowRect rect, out Rect bounds)
        {
            bounds = default;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            if (!TryProjectPopupCorner(cmr, new Vector2(rect.x, rect.y), ref minX, ref minY, ref maxX, ref maxY) ||
                !TryProjectPopupCorner(cmr, new Vector2(rect.xMax, rect.y), ref minX, ref minY, ref maxX, ref maxY) ||
                !TryProjectPopupCorner(cmr, new Vector2(rect.xMax, rect.yMax), ref minX, ref minY, ref maxX, ref maxY) ||
                !TryProjectPopupCorner(cmr, new Vector2(rect.x, rect.yMax), ref minX, ref minY, ref maxX, ref maxY))
            {
                return false;
            }

            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        bool TryProjectPopupCorner(
            Camera cmr,
            Vector2 uiPosition,
            ref float minX,
            ref float minY,
            ref float maxX,
            ref float maxY)
        {
            Vector3 screen = cmr.WorldToScreenPoint(transform.TransformPoint(UIToLocal(uiPosition)));

            if (screen.z <= 0.0001f || !IsFinite(screen))
                return false;

            minX = Mathf.Min(minX, screen.x);
            minY = Mathf.Min(minY, screen.y);
            maxX = Mathf.Max(maxX, screen.x);
            maxY = Mathf.Max(maxY, screen.y);
            return true;
        }

        static Vector2 CalculateScreenFitDelta(Rect rect, Rect bounds)
        {
            const float margin = 1f;

            float minX = bounds.xMin + margin;
            float maxX = bounds.xMax - margin;
            float minY = bounds.yMin + margin;
            float maxY = bounds.yMax - margin;
            float targetWidth = Mathf.Max(0f, maxX - minX);
            float targetHeight = Mathf.Max(0f, maxY - minY);
            float dx = 0f;
            float dy = 0f;

            if (rect.width <= targetWidth)
            {
                if (rect.xMin < minX)
                    dx = minX - rect.xMin;
                else if (rect.xMax > maxX)
                    dx = maxX - rect.xMax;
            }
            else
            {
                dx = bounds.center.x - rect.center.x;
            }

            if (rect.height <= targetHeight)
            {
                if (rect.yMin < minY)
                    dy = minY - rect.yMin;
                else if (rect.yMax > maxY)
                    dy = maxY - rect.yMax;
            }
            else
            {
                dy = bounds.center.y - rect.center.y;
            }

            return new Vector2(dx, dy);
        }

        bool TryScreenDeltaToUIDelta(Camera cmr, Vector2 uiPosition, Vector2 screenDelta, out Vector2 uiDelta)
        {
            uiDelta = default;
            Vector3 centerScreen = cmr.WorldToScreenPoint(transform.TransformPoint(UIToLocal(uiPosition)));

            if (centerScreen.z <= 0.0001f || !IsFinite(centerScreen))
                return false;

            var targetScreen = new Vector3(centerScreen.x + screenDelta.x, centerScreen.y + screenDelta.y, centerScreen.z);
            var plane = new Plane(transform.forward, transform.position);
            var ray = cmr.ScreenPointToRay(targetScreen);

            if (!plane.Raycast(ray, out float distance) || distance < 0f)
                return false;

            uiDelta = LocalToUI(transform.InverseTransformPoint(ray.GetPoint(distance))) - uiPosition;
            return true;
        }

        Vector3 UIToLocal(Vector2 uiPosition, Vector2 currentSize, float ppu)
        {
            return new Vector3(
                (uiPosition.x - currentSize.x * _pivot.x) / ppu,
                (currentSize.y * (1f - _pivot.y) - uiPosition.y) / ppu,
                0f);
        }

        float ResolveScreenPixelsPerUIUnit(Vector2 currentSize)
        {
            var cmr = ResolveCamera();

            if (!cmr)
                return 1f;

            currentSize = SanitizeSize(currentSize);
            float ppu = SanitizePixelsPerUnit(_pixelsPerUnit);
            var centerUi = new Vector2(currentSize.x * 0.5f, currentSize.y * 0.5f);
            var centerWorld = transform.TransformPoint(UIToLocal(centerUi, currentSize, ppu));
            float scale = 0f;
            int sampleCount = 0;

            if (TryMeasureScreenDistance(
                    cmr,
                    centerWorld,
                    transform.TransformPoint(UIToLocal(centerUi + Vector2.right, currentSize, ppu)),
                    out float xScale))
            {
                scale += xScale;
                ++sampleCount;
            }

            if (TryMeasureScreenDistance(
                    cmr,
                    centerWorld,
                    transform.TransformPoint(UIToLocal(centerUi + Vector2.up, currentSize, ppu)),
                    out float yScale))
            {
                scale += yScale;
                ++sampleCount;
            }

            return sampleCount > 0
                ? SanitizeUIScale((scale / sampleCount) / SanitizeAntiAliasSmoothing(_antiAliasSmoothing))
                : 1f;
        }

        static bool TryMeasureScreenDistance(Camera cmr, Vector3 fromWorld, Vector3 toWorld, out float distance)
        {
            distance = 0f;

            var fromScreen = cmr.WorldToScreenPoint(fromWorld);
            var toScreen = cmr.WorldToScreenPoint(toWorld);

            if (fromScreen.z <= 0f || toScreen.z <= 0f)
                return false;

            if (!IsFinite(fromScreen) || !IsFinite(toScreen))
                return false;

            distance = Vector2.Distance(fromScreen, toScreen);
            return distance > 0f && !float.IsNaN(distance) && !float.IsInfinity(distance);
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        static float SanitizeUIScale(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? Mathf.Max(0.0001f, value)
                : 1f;
        }

        public bool TryScreenPointToSurface(Vector2 screenPosition, out Vector2 surfacePosition)
        {
            return TryScreenPointToSurface(screenPosition, out surfacePosition, out _);
        }

        internal bool TryScreenPointToSurface(Vector2 screenPosition, out Vector2 surfacePosition, out float distance)
        {
            surfacePosition = default;
            distance = float.PositiveInfinity;

            var cmr = ResolveCamera();

            if (!cmr)
                return false;

            var resolution = ResolveInputRay(cmr, screenPosition);

            if (resolution.owner == this)
            {
                surfacePosition = resolution.ownerSurfacePosition;
                distance = resolution.ownerDistance;
                return true;
            }

            if (resolution.owner)
                return false;

            if (!TryRayToSurface(resolution.ray, out surfacePosition, out distance))
                return false;

            if (_depthMode == NowWorldDepthMode.SceneOccluded && IsSceneBlocked(resolution.sceneBlockDistance, distance))
                return false;

            return true;
        }

        protected virtual void DrawNowUI(NowRect rect)
        {
            rebuildNowUI?.Invoke(this, rect);
        }

        protected virtual INowInputProvider GetInputProvider()
        {
            _inputProvider ??= new NowWorldInputProvider();
            _inputProvider.graphic = this;
            _inputProvider.camera = ResolveCamera();
            _inputProvider.acceptNavigation = _acceptNavigation;
            _inputProvider.blockedWhenPointerOverUGUI = _blockedWhenPointerOverUGUI;
            return _inputProvider;
        }

        protected virtual Vector3 DeformVertex(in NowWorldVertex vertex)
        {
            return _deformer ? _deformer.Deform(vertex) : vertex.localPosition;
        }

        Vector2 ResolveLayoutAutoSize(Vector2 availableSize)
        {
            Vector2 measured;

            using (NowInput.BeginMeasurement(GetInputProvider(), new NowInputSurface(availableSize)))
            using (NowControls.IdScope(GetScopeId()))
            {
                measured = MeasureLayoutContent(availableSize);
            }

            var resolved = availableSize;

            if ((_layoutAutoSizeAxes & NowWorldAutoSizeAxes.Width) != 0 && measured.x > 0f)
                resolved.x = measured.x;

            if ((_layoutAutoSizeAxes & NowWorldAutoSizeAxes.Height) != 0 && measured.y > 0f)
                resolved.y = measured.y;

            resolved = SanitizeSize(resolved);

            if ((resolved - _size).sqrMagnitude > 0.0001f)
            {
                _size = resolved;
                _inputProvider?.ResetPosition();
                InvalidateInputResolution();
            }

            return resolved;
        }

        Vector2 MeasureLayoutContent(Vector2 availableSize)
        {
            var content = new FrameContent(this);
            return NowFrame.MeasureContent(
                ref content,
                new NowRect(0f, 0f, availableSize.x, availableSize.y));
        }

        protected virtual void OnEnable()
        {
            _glassBackdropMode = NowWorldGlassBackdrop.NormalizeMode(_glassBackdropMode);

            if (!_instances.Contains(this))
            {
                _instances.Add(this);
                InvalidateInputResolution();
            }

            _repaintTracker.Reset();
            EnsureRuntimeObjects();
            MarkDirty();
            QueueEditorRebuild();
        }

        protected virtual void OnDisable()
        {
            CancelEditorRebuild();

            if (_instances.Remove(this))
                InvalidateInputResolution();

            _repaintTracker.Reset();
            ClearRendererState();
        }

        protected virtual void OnDestroy()
        {
            CancelEditorRebuild();

            if (_instances.Remove(this))
                InvalidateInputResolution();

            ClearRendererState();
            ReleaseMaterials();

            if (_drawList != null)
            {
                _drawList.Dispose();
                _drawList = null;
            }
        }

        protected virtual void LateUpdate()
        {
            ApplyFacing();
            RegisterGlassBackdropIfNeeded();

            bool needsRebuild = _dirty || _rebuildEveryFrame || _repaintTracker.wantsRepaint;

            if (!needsRebuild && _autoRebuildOnInteraction)
            {
                if (!IsVisibleForRebuild())
                    return;

                needsRebuild = HasInteractionInputChanged();
            }

            if (!needsRebuild)
                return;

            if (!IsVisibleForRebuild())
                return;

            RebuildNowUI();
            RegisterGlassBackdropIfNeeded();
        }

    #if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            _size = SanitizeSize(_size);
            _pixelsPerUnit = SanitizePixelsPerUnit(_pixelsPerUnit);
            _antiAliasSmoothing = SanitizeAntiAliasSmoothing(_antiAliasSmoothing);
            _layoutAutoSizeAxes &= NowWorldAutoSizeAxes.Both;
            _glassBackdropMode = NowWorldGlassBackdrop.NormalizeMode(_glassBackdropMode);
            MarkDirty();
            _inputProvider?.ResetPosition();
            InvalidateInputResolution();
            QueueEditorRebuild();
        }
    #endif

        void EnsureRuntimeObjects()
        {
            if (!_meshFilter)
                _meshFilter = GetComponent<MeshFilter>();

            if (!_meshRenderer)
                _meshRenderer = GetComponent<MeshRenderer>();

            _drawList ??= new NowDrawList();

            if (_meshFilter && _meshFilter.sharedMesh != _drawList.mesh)
                _meshFilter.sharedMesh = _drawList.mesh;
        }

        int GetScopeId()
        {
            if (_scopeId != 0)
                return _scopeId;

            _scopeId = ++_nextScopeId;
            return _scopeId;
        }

        void ApplyWorldTransform()
        {
            var targetMesh = _drawList.mesh;

            if (!targetMesh || targetMesh.vertexCount == 0)
                return;

            targetMesh.GetVertices(_vertices);

            var currentSize = SanitizeSize(_size);

            for (int i = 0; i < _vertices.Count; ++i)
            {
                var source = _vertices[i];
                var ui = new Vector2(source.x, -source.y);
                var local = UIToLocal(ui);
                local.z = source.z / SanitizePixelsPerUnit(_pixelsPerUnit);
                var normalized = new Vector2(
                    currentSize.x > 0f ? Mathf.Clamp01(ui.x / currentSize.x) : 0f,
                    currentSize.y > 0f ? Mathf.Clamp01(ui.y / currentSize.y) : 0f);

                _vertices[i] = DeformVertex(new NowWorldVertex(i, ui, normalized, local));
            }

            targetMesh.SetVertices(_vertices);
            targetMesh.RecalculateBounds();
        }

        void ApplyRendererState()
        {
            if (!_meshRenderer)
                return;

            if (_drawList is not { hasGeometry: true })
            {
                _hasGlassBatches = false;
                _maxGlassBlurRadius = 0f;
                _maxGlassBlurQuality = NowGlassBlurQuality.Balanced;

                if (_sharedMaterials.Count > 0 || !_sharedMaterialsAssigned)
                {
                    _sharedMaterials.Clear();
                    _meshRenderer.SetSharedMaterials(_sharedMaterials);
                    _sharedMaterialsAssigned = true;
                }

                return;
            }

            var batches = _drawList.batches;
            int count = batches.Count;
            _hasGlassBatches = false;
            _maxGlassBlurRadius = 0f;
            _maxGlassBlurQuality = NowGlassBlurQuality.Balanced;
            bool materialsChanged = !_sharedMaterialsAssigned || _sharedMaterials.Count != count;

            if (_sharedMaterials.Count > count)
                _sharedMaterials.RemoveRange(count, _sharedMaterials.Count - count);

            while (_sharedMaterials.Count < count)
                _sharedMaterials.Add(null);

            for (int i = 0; i < count; ++i)
            {
                var batch = batches[i];

                if (batch.kind == NowMeshKind.Glass)
                {
                    _hasGlassBatches = true;
                    _maxGlassBlurRadius = Mathf.Max(_maxGlassBlurRadius, batch.data.x);
                    _maxGlassBlurQuality = MaxQuality(_maxGlassBlurQuality, NowGlassRenderer.GetBatchQuality(batch));
                }

                var material = GetMaterial(batch);

                if (!ReferenceEquals(_sharedMaterials[i], material))
                {
                    _sharedMaterials[i] = material;
                    materialsChanged = true;
                }
            }

            if (materialsChanged)
            {
                _meshRenderer.SetSharedMaterials(_sharedMaterials);
                _sharedMaterialsAssigned = true;
            }
        }

        void RegisterGlassBackdropIfNeeded()
        {
            if (!_hasGlassBatches)
            {
                ApplyGlassBackdropTexture(null);
                return;
            }

            var cmr = ResolveCamera();

            if (!cmr)
            {
                ApplyGlassBackdropTexture(null);
                return;
            }

            _glassBackdropMode = NowWorldGlassBackdrop.NormalizeMode(_glassBackdropMode);
            bool usesSceneDepth = UsesGlassSceneDepth();

            if (usesSceneDepth)
                NowWorldGlassBackdrop.RequestSceneDepth(cmr);

            if (_glassBackdropMode == NowWorldGlassBackdropMode.TintOnly)
            {
                ApplyGlassBackdropTexture(null);
                return;
            }

            NowWorldGlassBackdrop.Register(
                cmr,
                this,
                _glassBackdropMode,
                _maxGlassBlurRadius,
                _maxGlassBlurQuality,
                usesSceneDepth);
        }

        internal float GetCameraDepth(Camera camera)
        {
            if (!camera)
                return float.PositiveInfinity;

            return Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
        }

        internal static void CollectBackdropContributors(
            Camera camera,
            NowWorldGraphic requester,
            float requesterDepth,
            List<NowWorldGraphic> results)
        {
            results.Clear();

            if (camera == null)
                return;

            var frustumPlanes = GetFrustumPlanes(camera);

            for (int i = _instances.Count - 1; i >= 0; --i)
            {
                var graphic = _instances[i];

                if (!graphic)
                {
                    _instances.RemoveAt(i);
                    InvalidateInputResolution();
                    continue;
                }

                if (ReferenceEquals(graphic, requester))
                    continue;

                if (!graphic.enabled ||
                    !graphic.gameObject.activeInHierarchy ||
                    !graphic._meshRenderer ||
                    !graphic._meshRenderer.enabled ||
                    graphic._drawList is not { hasGeometry: true })
                {
                    continue;
                }

                if (graphic.ResolveCamera() != camera)
                    continue;

                if (!IsLayerVisible(camera, graphic.gameObject.layer))
                    continue;

                if (!graphic.IsInsideFrustum(frustumPlanes))
                    continue;

                if (graphic.GetCameraDepth(camera) <= requesterDepth + InputOcclusionEpsilon)
                    continue;

                results.Add(graphic);
            }

            _backdropSortCamera = camera;
            results.Sort(CompareBackdropContributorDepth);
            _backdropSortCamera = null;
        }

        internal void DrawBackdropContribution(CommandBuffer commandBuffer)
        {
            if (commandBuffer == null ||
                !_meshRenderer ||
                _drawList is not { hasGeometry: true })
            {
                return;
            }

            var batches = _drawList.batches;
            int count = Mathf.Min(batches.Count, _sharedMaterials.Count);

            for (int i = 0; i < count; ++i)
            {
                if (batches[i].kind == NowMeshKind.Glass || !_sharedMaterials[i])
                    continue;

                commandBuffer.DrawRenderer(_meshRenderer, _sharedMaterials[i], i, 0);
            }
        }

        internal void ApplyGlassBackdropTexture(Texture texture)
        {
            ApplyGlassBackdropTexture(texture, texture);
        }

        internal void ApplyGlassBackdropTexture(Texture texture, Texture sharpTexture)
        {
            _glassBackdropTexture = texture;
            _glassSharpBackdropTexture = sharpTexture ? sharpTexture : texture;
            bool useBackdrop = texture && glassBackdropMode != NowWorldGlassBackdropMode.TintOnly;
            var fallback = texture ? texture : Texture2D.blackTexture;
            var sharpFallback = _glassSharpBackdropTexture ? _glassSharpBackdropTexture : fallback;

            if (_meshRenderer)
                _meshRenderer.SetPropertyBlock(null);

            if (_drawList is not { hasGeometry: true })
                return;

            var batches = _drawList.batches;
            int count = Mathf.Min(batches.Count, _sharedMaterials.Count);

            for (int i = 0; i < count; ++i)
            {
                if (batches[i].kind != NowMeshKind.Glass)
                    continue;

                var material = _sharedMaterials[i];
                bool batchUsesBackdrop = useBackdrop;

                if (!material)
                    continue;

                material.SetFloat(_nowMaterialGlassModeId, 1f);
                material.SetTexture(_nowBackdropTexId, fallback);
                material.SetTexture(_nowGlassSharpBackdropTexId, sharpFallback);
                material.SetVector(_nowBackdropUvTransformId, new Vector4(1f, 1f, 0f, 0f));
                material.SetFloat(_nowGlassUseBackdropId, batchUsesBackdrop ? 1f : 0f);
                ApplyGlassDepthProperties(material);
            }
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

        static int CompareBackdropContributorDepth(NowWorldGraphic lhs, NowWorldGraphic rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return 0;

            if (!lhs)
                return 1;

            if (!rhs)
                return -1;

            float lhsDepth = lhs.GetCameraDepth(_backdropSortCamera);
            float rhsDepth = rhs.GetCameraDepth(_backdropSortCamera);
            return rhsDepth.CompareTo(lhsDepth);
        }

        Material GetMaterial(NowMeshBatch batch)
        {
            var source = batch.material;

            if (!source)
                return null;

            if (!_materials.TryGetValue(source, out var material) || !material)
            {
                material = new Material(source)
                {
                    name = source.name + " World",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _materials[source] = material;
            }
            else
            {
                material.CopyPropertiesFromMaterial(source);
            }

            ApplyDepthMode(material);
            ApplyGlassBackdropMode(material, batch);
            return material;
        }

        void ApplyGlassBackdropMode(Material material, NowMeshBatch batch)
        {
            if (!material || batch.kind != NowMeshKind.Glass)
                return;

            bool useBackdrop = _glassBackdropTexture &&
                glassBackdropMode != NowWorldGlassBackdropMode.TintOnly;

            material.SetFloat(_nowMaterialGlassModeId, 1f);
            material.SetTexture(_nowBackdropTexId, _glassBackdropTexture ? _glassBackdropTexture : Texture2D.blackTexture);
            material.SetTexture(
                _nowGlassSharpBackdropTexId,
                _glassSharpBackdropTexture ? _glassSharpBackdropTexture :
                    _glassBackdropTexture ? _glassBackdropTexture : Texture2D.blackTexture);
            material.SetVector(_nowBackdropUvTransformId, new Vector4(1f, 1f, 0f, 0f));
            material.SetFloat(_nowGlassUseBackdropId, useBackdrop ? 1f : 0f);
            ApplyGlassDepthProperties(material);
        }

        void ApplyGlassDepthProperties(Material material)
        {
            if (!material)
                return;

            bool useSceneDepth = UsesGlassSceneDepth();
            material.SetFloat(_nowGlassUseSceneDepthId, useSceneDepth ? 1f : 0f);
            material.SetFloat(_nowGlassDepthEpsilonId, GlassDepthEpsilon);
        }

        bool UsesGlassSceneDepth()
        {
            return NowWorldGlassBackdrop.NormalizeMode(_glassBackdropMode) != NowWorldGlassBackdropMode.TintOnly &&
                _maxGlassBlurRadius >= GlassSceneDepthBlurThreshold;
        }

        void ApplyDepthMode(Material material)
        {
            if (!material || !material.HasProperty(_zTestId))
                return;

            material.SetFloat(
                _zTestId,
                _depthMode == NowWorldDepthMode.SceneOccluded
                    ? (float)CompareFunction.LessEqual
                    : (float)CompareFunction.Always);
        }

        bool HasInteractionInputChanged()
        {
            var surface = new NowInputSurface(SanitizeSize(_size));
            return _repaintTracker.HasInputChanged(GetInputProvider(), surface);
        }

        void ApplyFacing()
        {
            if (_facingMode == NowWorldFacingMode.None)
                return;

            var cmr = ResolveCamera();

            if (!cmr)
                return;

            var direction = transform.position - cmr.transform.position;

            if (_facingMode == NowWorldFacingMode.FaceCameraYOnly)
            {
                direction.y = 0f;

                if (direction.sqrMagnitude <= 0.000001f)
                    return;

                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                return;
            }

            if (direction.sqrMagnitude <= 0.000001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, cmr.transform.up);
        }

        /// <summary>
        /// Honors the user-assigned camera when set; otherwise falls back to
        /// <see cref="Camera.main"/> cached in a non-serialized field so the
        /// serialized reference is never mutated, re-resolving when the cached
        /// fallback dies.
        /// </summary>
        Camera ResolveCamera()
        {
            if (_targetCamera)
                return _targetCamera;

            if (!_fallbackCamera)
                _fallbackCamera = Camera.main;

            return _fallbackCamera;
        }

        bool TryRayToSurface(Ray ray, out Vector2 surfacePosition, out float distance)
        {
            surfacePosition = default;
            distance = default;

            var plane = new Plane(transform.forward, transform.position);

            if (!plane.Raycast(ray, out distance) || distance < 0f)
                return false;

            surfacePosition = LocalToUI(transform.InverseTransformPoint(ray.GetPoint(distance)));
            return true;
        }

        static InputRayResolution ResolveInputRay(Camera cmr, Vector2 screenPosition)
        {
            int frame = Time.frameCount;

            if (_inputRayResolution.valid &&
                _inputRayResolution.frame == frame &&
                _inputRayResolution.version == _inputResolverVersion &&
                _inputRayResolution.camera == cmr &&
                (_inputRayResolution.screenPosition - screenPosition).sqrMagnitude <= 0.0001f)
            {
                return _inputRayResolution;
            }

            var resolution = new InputRayResolution
            {
                valid = true,
                frame = frame,
                version = _inputResolverVersion,
                camera = cmr,
                screenPosition = screenPosition,
                ray = cmr.ScreenPointToRay(screenPosition),
                owner = null,
                ownerSurfacePosition = default,
                ownerDistance = float.PositiveInfinity,
                sceneBlockDistance = float.PositiveInfinity
            };

            var frustumPlanes = GetFrustumPlanes(cmr);
            resolution.sceneBlockDistance = FindSceneBlockDistance(cmr, resolution.ray);

            for (int i = _instances.Count - 1; i >= 0; --i)
            {
                var other = _instances[i];

                if (!other)
                {
                    _instances.RemoveAt(i);
                    InvalidateInputResolution();
                    continue;
                }

                if (!other.enabled || !other.gameObject.activeInHierarchy)
                    continue;

                if (other.ResolveCamera() != cmr)
                    continue;

                if (!IsLayerVisible(cmr, other.gameObject.layer))
                    continue;

                if (!other.IsInsideFrustum(frustumPlanes))
                    continue;

                if (!other.TryRayToSurface(resolution.ray, out var surfacePosition, out float distance))
                    continue;

                if (!other.ContainsSurfacePoint(surfacePosition) &&
                    !NowOverlay.IsPointerInsideOverlay(other, surfacePosition))
                {
                    continue;
                }

                if (other._depthMode == NowWorldDepthMode.SceneOccluded &&
                    IsSceneBlocked(resolution.sceneBlockDistance, distance))
                {
                    continue;
                }

                if (distance >= resolution.ownerDistance - InputOcclusionEpsilon)
                    continue;

                resolution.owner = other;
                resolution.ownerSurfacePosition = surfacePosition;
                resolution.ownerDistance = distance;
            }

            _inputRayResolution = resolution;
            return _inputRayResolution;
        }

        static float FindSceneBlockDistance(Camera cmr, Ray ray)
        {
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _sceneOcclusionHits,
                cmr.farClipPlane,
                cmr.cullingMask,
                QueryTriggerInteraction.Ignore);

            var nearest = float.PositiveInfinity;

            for (int i = 0; i < hitCount; ++i)
            {
                var collider = _sceneOcclusionHits[i].collider;

                if (!collider)
                    continue;

                var colliderTransform = collider.transform;

                if (IsWorldGraphicTransform(colliderTransform))
                    continue;

                if (_sceneOcclusionHits[i].distance < nearest)
                    nearest = _sceneOcclusionHits[i].distance;
            }

            return nearest;
        }

        bool IsVisibleForRebuild()
        {
            if (!_frustumCullRebuilds)
                return true;

            var cmr = ResolveCamera();

            if (!cmr)
                return true;

            if (!IsLayerVisible(cmr, gameObject.layer))
                return false;

            return IsInsideFrustum(GetFrustumPlanes(cmr));
        }

        /// <summary>
        /// Frustum planes cached per camera per frame, mirroring how
        /// <see cref="ResolveInputRay"/> caches its ray resolution, so rebuild
        /// culling, input hit-testing and backdrop collection share one
        /// <see cref="GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])"/> call.
        /// </summary>
        static Plane[] GetFrustumPlanes(Camera cmr)
        {
            int frame = Time.frameCount;

            if (_frustumPlanesCamera == cmr && _frustumPlanesFrame == frame)
                return _frustumPlanes;

            GeometryUtility.CalculateFrustumPlanes(cmr, _frustumPlanes);
            _frustumPlanesCamera = cmr;
            _frustumPlanesFrame = frame;
            return _frustumPlanes;
        }

        static bool IsLayerVisible(Camera cmr, int layer)
        {
            return (cmr.cullingMask & (1 << layer)) != 0;
        }

        bool IsInsideFrustum(Plane[] planes)
        {
            return GeometryUtility.TestPlanesAABB(planes, CalculateCullingBounds());
        }

        Bounds CalculateCullingBounds()
        {
            var bounds = CalculateWorldBounds();

            if (_meshRenderer && _meshRenderer.enabled)
            {
                var rendererBounds = _meshRenderer.bounds;

                if (rendererBounds.size.sqrMagnitude > 0f)
                    bounds.Encapsulate(rendererBounds);
            }

            return bounds;
        }

        Bounds CalculateWorldBounds()
        {
            var currentSize = SanitizeSize(_size);
            var bounds = new Bounds(transform.TransformPoint(UIToLocal(Vector2.zero)), Vector3.zero);

            bounds.Encapsulate(transform.TransformPoint(UIToLocal(new Vector2(currentSize.x, 0f))));
            bounds.Encapsulate(transform.TransformPoint(UIToLocal(new Vector2(0f, currentSize.y))));
            bounds.Encapsulate(transform.TransformPoint(UIToLocal(currentSize)));
            bounds.Expand(0.001f);
            return bounds;
        }

        static bool IsSceneBlocked(float sceneBlockDistance, float surfaceDistance)
        {
            return sceneBlockDistance < surfaceDistance - InputOcclusionEpsilon;
        }

        static bool IsWorldGraphicTransform(Transform candidate)
        {
            for (int i = _instances.Count - 1; i >= 0; --i)
            {
                var graphic = _instances[i];

                if (!graphic)
                {
                    _instances.RemoveAt(i);
                    InvalidateInputResolution();
                    continue;
                }

                if (candidate == graphic.transform || candidate.IsChildOf(graphic.transform))
                    return true;
            }

            return false;
        }

        static void InvalidateInputResolution()
        {
            unchecked
            {
                ++_inputResolverVersion;
            }

            _inputRayResolution.valid = false;
        }

        bool ContainsSurfacePoint(Vector2 surfacePosition)
        {
            var currentSize = SanitizeSize(_size);
            return surfacePosition.x >= 0f && surfacePosition.x <= currentSize.x &&
                   surfacePosition.y >= 0f && surfacePosition.y <= currentSize.y;
        }

        void ClearRendererState()
        {
            _hasGlassBatches = false;
            _maxGlassBlurRadius = 0f;
            _maxGlassBlurQuality = NowGlassBlurQuality.Balanced;
            _sharedMaterials.Clear();
            _sharedMaterialsAssigned = false;

            if (_meshRenderer != null)
                _meshRenderer.SetSharedMaterials(_sharedMaterials);

            if (_meshFilter != null && _meshFilter.sharedMesh == _drawList?.mesh)
                _meshFilter.sharedMesh = null;
        }

        void ReleaseMaterials()
        {
            foreach (var material in _materials.Values)
            {
                if (material == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }

            _materials.Clear();
            _sharedMaterials.Clear();
            _sharedMaterialsAssigned = false;
        }

        static Vector2 SanitizeSize(Vector2 value)
        {
            return new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y));
        }

        static float SanitizePixelsPerUnit(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 100f;
        }

        static float SanitizeAntiAliasSmoothing(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 1f;
        }

#if UNITY_EDITOR
        void QueueEditorRebuild()
        {
            if (NowEditorRebuildQueue.Queue(ref _editorRebuildQueued, RunQueuedEditorRebuild))
                EnsureEditorCallbacks();
        }

        void CancelEditorRebuild()
        {
            NowEditorRebuildQueue.Cancel(ref _editorRebuildQueued, RunQueuedEditorRebuild);
        }

        void RunQueuedEditorRebuild()
        {
            _editorRebuildQueued = false;

            if (!this || Application.isPlaying || !isActiveAndEnabled)
                return;

            RebuildNowUI();
            RegisterGlassBackdropIfNeeded();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        static void EnsureEditorCallbacks()
        {
            if (_editorCallbacksRegistered)
                return;

            _editorCallbacksRegistered = true;
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
        }

        static void OnEditorPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
                return;

            NowWorldGlassBackdrop.ResetEditorPreviewState();

            for (int i = _instances.Count - 1; i >= 0; --i)
            {
                var graphic = _instances[i];

                if (!graphic)
                {
                    _instances.RemoveAt(i);
                    continue;
                }

                if (!graphic.isActiveAndEnabled)
                    continue;

                graphic.ApplyGlassBackdropTexture(null);
                graphic.MarkDirty();
                graphic.QueueEditorRebuild();
            }
        }
#else
        void QueueEditorRebuild()
        {
        }

        void CancelEditorRebuild()
        {
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
#if UNITY_EDITOR
            if (_editorCallbacksRegistered)
                EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;

            _editorCallbacksRegistered = false;
#endif
            _nextScopeId = 0;
            _inputResolverVersion = 0;
            _instances.Clear();
            _inputRayResolution = default;
            _frustumPlanesCamera = null;
            _frustumPlanesFrame = -1;
        }
    }
}
