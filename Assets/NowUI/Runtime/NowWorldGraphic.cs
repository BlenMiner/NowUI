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

    public sealed class NowWorldInputProvider : INowInputProvider
    {
        NowWorldGraphic _graphic;
        Transform _transform;
        Camera _camera;
        Vector2 _size = new Vector2(200f, 80f);
        Vector2 _pivot = new Vector2(0.5f, 0.5f);
        float _pixelsPerUnit = 100f;
        bool _acceptNavigation;
        int _lastFrame = -1;
        bool _hasPreviousPosition;
        Vector2 _previousPosition;
        NowInputSnapshot _snapshot;
        bool _rawInputAvailable;

        public NowWorldGraphic graphic
        {
            get => _graphic;
            set
            {
                if (_graphic == value)
                    return;

                _graphic = value;
                ResetPosition();
            }
        }

        public Transform transform
        {
            get => _transform;
            set
            {
                if (_transform == value)
                    return;

                _transform = value;
                ResetPosition();
            }
        }

        public Camera camera
        {
            get => _camera;
            set => _camera = value;
        }

        public Vector2 size
        {
            get => _graphic ? _graphic.size : _size;
            set => _size = SanitizeSize(value);
        }

        public Vector2 pivot
        {
            get => _graphic ? _graphic.pivot : _pivot;
            set => _pivot = value;
        }

        public float pixelsPerUnit
        {
            get => _graphic ? _graphic.pixelsPerUnit : _pixelsPerUnit;
            set => _pixelsPerUnit = SanitizePixelsPerUnit(value);
        }

        public bool acceptNavigation
        {
            get => _graphic ? _graphic.acceptNavigation : _acceptNavigation;
            set => _acceptNavigation = value;
        }

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            int frame = Time.frameCount;

            if (_lastFrame != frame)
            {
                _lastFrame = frame;

                if (NowMouseInput.TryGet(out var input))
                    _rawInputAvailable = TryGetSnapshot(surface, input, out _snapshot);
                else
                {
                    _snapshot = default;
                    _rawInputAvailable = false;
                }
            }

            snapshot = _snapshot;
            return _rawInputAvailable;
        }

        internal bool TryGetSnapshot(NowInputSurface surface, NowMouseInput input, out NowInputSnapshot snapshot)
        {
            if (!input.hasPointer)
            {
                _hasPreviousPosition = false;
                snapshot = CreateSnapshot(false, default, default, default, input);
                return true;
            }

            bool hit = TryScreenPointToSurface(input.screenPosition, out var position);
            bool inside = hit &&
                          position is { x: >= 0f, y: >= 0f } &&
                          position.x <= surface.size.x &&
                          position.y <= surface.size.y;
            bool hasPointer = hit && (inside ||
                input.pointerButtonsDown != NowPointerButtons.None ||
                input.pointerButtonsReleased != NowPointerButtons.None);

            var previous = _hasPreviousPosition ? _previousPosition : position;
            var delta = hit ? position - previous : default;

            if (hit)
            {
                _previousPosition = position;
                _hasPreviousPosition = true;
            }
            else switch (_hasPreviousPosition)
            {
                case true when
                    input.pointerButtonsReleased != NowPointerButtons.None:
                    position = _previousPosition;
                    previous = _previousPosition;
                    hasPointer = true;
                    _hasPreviousPosition = false;
                    break;
                case true when
                    input.pointerButtonsDown != NowPointerButtons.None &&
                    input.pointerButtonsPressed == NowPointerButtons.None:
                    position = _previousPosition;
                    previous = _previousPosition;
                    hasPointer = true;
                    break;
                default:
                    _hasPreviousPosition = false;
                    previous = default;
                    position = default;
                    break;
            }

            snapshot = CreateSnapshot(hasPointer, position, previous, delta, input);
            return true;
        }

        public bool TryScreenPointToSurface(Vector2 screenPosition, out Vector2 surfacePosition)
        {
            if (_graphic)
                return _graphic.TryScreenPointToSurface(screenPosition, out surfacePosition);

            surfacePosition = default;

            var targetTransform = _transform;
            var targetCamera = ResolveCamera();

            if (!targetTransform || !targetCamera)
                return false;

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(targetTransform.forward, targetTransform.position);

            if (!plane.Raycast(ray, out float distance))
                return false;

            var local = targetTransform.InverseTransformPoint(ray.GetPoint(distance));
            float ppu = SanitizePixelsPerUnit(_pixelsPerUnit);
            var targetSize = SanitizeSize(_size);
            surfacePosition = new Vector2(
                local.x * ppu + targetSize.x * _pivot.x,
                targetSize.y * (1f - _pivot.y) - local.y * ppu);
            return true;
        }

        public void ResetPosition()
        {
            _lastFrame = -1;
            _hasPreviousPosition = false;
            _previousPosition = default;
            _snapshot = default;
        }

        NowInputSnapshot CreateSnapshot(
            bool hasPointer,
            Vector2 position,
            Vector2 previous,
            Vector2 delta,
            NowMouseInput input)
        {
            bool navigation = acceptNavigation;

            return new NowInputSnapshot(
                hasPointer,
                position,
                previous,
                delta,
                input.pointerButtonsDown,
                input.pointerButtonsPressed,
                input.pointerButtonsReleased,
                input.scrollDelta,
                navigation ? input.navigation : Vector2.zero,
                navigation && input.focusPreviousPressed,
                navigation && input.focusNextPressed,
                navigation && input.submitDown,
                navigation && input.submitPressed,
                navigation && input.submitReleased,
                navigation && input.cancelDown,
                navigation && input.cancelPressed,
                navigation && input.cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);
        }

        Camera ResolveCamera()
        {
            if (_camera)
                return _camera;

            return Camera.main;
        }

        static Vector2 SanitizeSize(Vector2 value)
        {
            return new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y));
        }

        static float SanitizePixelsPerUnit(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 100f;
        }
    }

    [AddComponentMenu("NowUI/Now World Graphic")]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class NowWorldGraphic : MonoBehaviour
    {
        static readonly int _zTestId = Shader.PropertyToID("_ZTest");
        static readonly int _nowBackdropTexId = Shader.PropertyToID("_NowBackdropTex");
        static readonly int _nowBackdropUvTransformId = Shader.PropertyToID("_NowBackdropUVTransform");
        static readonly int _nowGlassSharpBackdropTexId = Shader.PropertyToID("_NowGlassSharpBackdropTex");
        static readonly int _nowGlassUseBackdropId = Shader.PropertyToID("_NowGlassUseBackdrop");
        static readonly int _nowGlassUseSceneDepthId = Shader.PropertyToID("_NowGlassUseSceneDepth");
        static readonly int _nowGlassDepthEpsilonId = Shader.PropertyToID("_NowGlassDepthEpsilon");
        const float InputOcclusionEpsilon = 0.0001f;
        const float GlassDepthEpsilon = 0.02f;
        const float GlassSceneDepthBlurThreshold = 0.25f;

        static int _nextScopeId;
        static int _inputResolverVersion;
        static readonly List<NowWorldGraphic> _instances = new List<NowWorldGraphic>(16);
        static readonly RaycastHit[] _sceneOcclusionHits = new RaycastHit[16];
        static readonly Plane[] _inputFrustumPlanes = new Plane[6];
        static readonly Plane[] _rebuildFrustumPlanes = new Plane[6];
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
        [NonSerialized] Material[] _sharedMaterials = Array.Empty<Material>();
        [NonSerialized] bool _dirty = true;
        [NonSerialized] bool _wantsInteractionRepaint;
        [NonSerialized] bool _hasLastInteractionInput;
        [NonSerialized] bool _hasGlassBatches;
        [NonSerialized] float _maxGlassBlurRadius;
        [NonSerialized] NowGlassBlurQuality _maxGlassBlurQuality = NowGlassBlurQuality.Balanced;
        [NonSerialized] int _scopeId;
        [NonSerialized] InteractionInputState _lastInteractionInput;
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

        struct InteractionInputState
        {
            const float PositionEpsilonSqr = 0.25f;

            private bool pointerInside;
            private Vector2 pointerPosition;
            private NowPointerButtons pointerButtonsDown;
            private NowPointerButtons pointerButtonsPressed;
            private NowPointerButtons pointerButtonsReleased;
            private Vector2 scrollDelta;
            private Vector2 navigation;
            private bool focusPreviousPressed;
            private bool focusNextPressed;
            private bool submitDown;
            private bool submitPressed;
            private bool submitReleased;
            private bool cancelDown;
            private bool cancelPressed;
            private bool cancelReleased;

            public static InteractionInputState FromSnapshot(NowInputSnapshot snapshot, Vector2 size)
            {
                bool inside = snapshot is { hasPointer: true, pointerPosition: { x: >= 0f, y: >= 0f } } &&
                              snapshot.pointerPosition.x <= size.x &&
                              snapshot.pointerPosition.y <= size.y;

                return new InteractionInputState
                {
                    pointerInside = inside,
                    pointerPosition = snapshot.pointerPosition,
                    pointerButtonsDown = snapshot.pointerButtonsDown,
                    pointerButtonsPressed = snapshot.pointerButtonsPressed,
                    pointerButtonsReleased = snapshot.pointerButtonsReleased,
                    scrollDelta = snapshot.scrollDelta,
                    navigation = snapshot.navigation,
                    focusPreviousPressed = snapshot.focusPreviousPressed,
                    focusNextPressed = snapshot.focusNextPressed,
                    submitDown = snapshot.submitDown,
                    submitPressed = snapshot.submitPressed,
                    submitReleased = snapshot.submitReleased,
                    cancelDown = snapshot.cancelDown,
                    cancelPressed = snapshot.cancelPressed,
                    cancelReleased = snapshot.cancelReleased
                };
            }

            public bool HasChangedSince(in InteractionInputState previous)
            {
                bool pointerRelevant =
                    pointerInside ||
                    previous.pointerInside ||
                    pointerButtonsDown != NowPointerButtons.None ||
                    previous.pointerButtonsDown != NowPointerButtons.None;

                if (pointerInside != previous.pointerInside)
                    return true;

                if (pointerRelevant && (pointerPosition - previous.pointerPosition).sqrMagnitude > PositionEpsilonSqr)
                    return true;

                if (pointerButtonsDown != previous.pointerButtonsDown)
                    return true;

                if (pointerButtonsPressed != NowPointerButtons.None ||
                    pointerButtonsReleased != NowPointerButtons.None)
                {
                    return true;
                }

                if (pointerRelevant && scrollDelta != Vector2.zero)
                    return true;

                if ((navigation - previous.navigation).sqrMagnitude > PositionEpsilonSqr)
                    return true;

                if (submitDown != previous.submitDown || cancelDown != previous.cancelDown)
                    return true;

                return focusPreviousPressed || focusNextPressed ||
                    submitPressed || submitReleased || cancelPressed || cancelReleased;
            }
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

        [Obsolete("World glass foreground protection is automatic. Setting this property has no effect.")]
        public NowWorldGlassDepthMode glassDepthMode
        {
            get => NowWorldGlassDepthMode.ClipForeground;
            set { }
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
            float previousScale = Now.uiScale;
            NowDrawScope scope = default;
            bool repaintTracking = false;

            try
            {
                Now.SetUIScale(ResolveScreenPixelsPerUIUnit(currentSize));
                NowControlState.BeginRepaintTracking();
                repaintTracking = true;

                if (_layoutAutoSizeAxes != NowWorldAutoSizeAxes.None)
                {
                    currentSize = ResolveLayoutAutoSize(currentSize);
                    Now.SetUIScale(ResolveScreenPixelsPerUIUnit(currentSize));
                }

                var surface = new NowInputSurface(currentSize);
                scope = _drawList.Begin(currentSize, _glassBlurQuality);

                using (NowInput.Begin(GetInputProvider(), surface))
                using (NowControls.IdScope(GetScopeId()))
                {
                    StoreLastInteractionInput(NowInput.current, currentSize);

                    if (useLayoutMeasurePass)
                    {
                        int layoutCounter = NowLayout.BeginMeasurePass();

                        try
                        {
                            DrawNowUI(new NowRect(0f, 0f, currentSize.x, currentSize.y));
                        }
                        finally
                        {
                            NowLayout.EndMeasurePass(layoutCounter);
                        }
                    }

                    DrawNowUI(new NowRect(0f, 0f, currentSize.x, currentSize.y));
                    NowOverlay.Flush();
                }

                _wantsInteractionRepaint = NowControlState.EndRepaintTracking();
                repaintTracking = false;
                scope.Dispose();
                ApplyWorldTransform();
                ApplyRendererState();
                _dirty = false;
            }
            catch (Exception ex)
            {
                if (repaintTracking)
                    NowControlState.EndRepaintTracking();

                scope.Cancel();
                _drawList.Clear();
                ApplyRendererState();
                Debug.LogException(ex, this);
            }
            finally
            {
                Now.SetUIScale(previousScale);
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

        static float SanitizeUIScale(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? Mathf.Max(0.0001f, value)
                : 1f;
        }

        public bool TryScreenPointToSurface(Vector2 screenPosition, out Vector2 surfacePosition)
        {
            surfacePosition = default;

            var cmr = ResolveCamera();

            if (!cmr)
                return false;

            var resolution = ResolveInputRay(cmr, screenPosition);

            if (resolution.owner == this)
            {
                surfacePosition = resolution.ownerSurfacePosition;
                return true;
            }

            if (resolution.owner)
                return false;

            if (!TryRayToSurface(resolution.ray, out surfacePosition, out float distance))
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
            return _inputProvider;
        }

        protected virtual Vector3 DeformVertex(in NowWorldVertex vertex)
        {
            return _deformer ? _deformer.Deform(vertex) : vertex.localPosition;
        }

        Vector2 ResolveLayoutAutoSize(Vector2 availableSize)
        {
            Vector2 measured;

            using (NowInput.Begin(GetInputProvider(), new NowInputSurface(availableSize)))
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
            int layoutCounter = NowLayout.BeginMeasurePass();
            bool tracking = false;

            try
            {
                NowLayout.BeginContentTracking();
                tracking = true;

                DrawNowUI(new NowRect(0f, 0f, availableSize.x, availableSize.y));

                tracking = false;
                return NowLayout.EndContentTracking();
            }
            finally
            {
                if (tracking)
                    NowLayout.EndContentTracking();

                NowLayout.EndMeasurePass(layoutCounter);
            }
        }

        protected virtual void OnEnable()
        {
            _glassBackdropMode = NowWorldGlassBackdrop.NormalizeMode(_glassBackdropMode);

            if (!_instances.Contains(this))
            {
                _instances.Add(this);
                InvalidateInputResolution();
            }

            _hasLastInteractionInput = false;
            EnsureRuntimeObjects();
            MarkDirty();
            QueueEditorRebuild();
        }

        protected virtual void OnDisable()
        {
            CancelEditorRebuild();

            if (_instances.Remove(this))
                InvalidateInputResolution();

            _hasLastInteractionInput = false;
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

            bool needsRebuild = _dirty || _rebuildEveryFrame || _wantsInteractionRepaint;

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
                _meshRenderer.sharedMaterials = Array.Empty<Material>();
                return;
            }

            var batches = _drawList.batches;
            int count = batches.Count;
            _hasGlassBatches = false;
            _maxGlassBlurRadius = 0f;
            _maxGlassBlurQuality = NowGlassBlurQuality.Balanced;

            if (_sharedMaterials.Length != count)
                _sharedMaterials = new Material[count];

            for (int i = 0; i < count; ++i)
            {
                var batch = batches[i];

                if (batch.kind == NowMeshKind.Glass)
                {
                    _hasGlassBatches = true;
                    _maxGlassBlurRadius = Mathf.Max(_maxGlassBlurRadius, batch.data.x);
                    _maxGlassBlurQuality = MaxQuality(_maxGlassBlurQuality, NowGlassRenderer.GetBatchQuality(batch));
                }

                _sharedMaterials[i] = GetMaterial(batch);
            }

            _meshRenderer.sharedMaterials = _sharedMaterials;
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

            GeometryUtility.CalculateFrustumPlanes(camera, _rebuildFrustumPlanes);

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

                if (!graphic.IsInsideFrustum(_rebuildFrustumPlanes))
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
            int count = Mathf.Min(batches.Count, _sharedMaterials.Length);

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

            if (_drawList is not { hasGeometry: true } || _sharedMaterials == null)
                return;

            var batches = _drawList.batches;
            int count = Mathf.Min(batches.Count, _sharedMaterials.Length);

            for (int i = 0; i < count; ++i)
            {
                if (batches[i].kind != NowMeshKind.Glass)
                    continue;

                var material = _sharedMaterials[i];
                bool batchUsesBackdrop = useBackdrop;

                if (!material)
                    continue;

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
            var provider = GetInputProvider();
            var currentSize = SanitizeSize(_size);

            if (!provider.TryGetSnapshot(new NowInputSurface(currentSize), out var snapshot))
                snapshot = default;

            var current = InteractionInputState.FromSnapshot(snapshot, currentSize);

            if (!_hasLastInteractionInput)
            {
                _lastInteractionInput = current;
                _hasLastInteractionInput = true;
                return false;
            }

            return current.HasChangedSince(_lastInteractionInput);
        }

        void StoreLastInteractionInput(NowInputSnapshot snapshot, Vector2 currentSize)
        {
            _lastInteractionInput = InteractionInputState.FromSnapshot(snapshot, currentSize);
            _hasLastInteractionInput = true;
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

        Camera ResolveCamera()
        {
            if (_targetCamera)
                return _targetCamera;
            _targetCamera = Camera.main;
            return _targetCamera;
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

            GeometryUtility.CalculateFrustumPlanes(cmr, _inputFrustumPlanes);
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

                if (!other.IsInsideFrustum(_inputFrustumPlanes))
                    continue;

                if (!other.TryRayToSurface(resolution.ray, out var surfacePosition, out float distance))
                    continue;

                if (!other.ContainsSurfacePoint(surfacePosition))
                    continue;

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

            GeometryUtility.CalculateFrustumPlanes(cmr, _rebuildFrustumPlanes);
            return IsInsideFrustum(_rebuildFrustumPlanes);
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

            if (_meshRenderer != null)
                _meshRenderer.sharedMaterials = Array.Empty<Material>();

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
            _sharedMaterials = Array.Empty<Material>();
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
            if (Application.isPlaying || _editorRebuildQueued)
                return;

            EnsureEditorCallbacks();
            _editorRebuildQueued = true;
            EditorApplication.delayCall += RunQueuedEditorRebuild;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void CancelEditorRebuild()
        {
            if (!_editorRebuildQueued)
                return;

            EditorApplication.delayCall -= RunQueuedEditorRebuild;
            _editorRebuildQueued = false;
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
        }
    }
}
