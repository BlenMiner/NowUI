using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    [AddComponentMenu("NowUI/Now Pipeline Graphic")]
    [ExecuteAlways]
    public class NowPipelineGraphic : MonoBehaviour
    {
        const int MissingConsumerWarningFrames = 120;

        static readonly List<NowPipelineGraphic> _graphics = new List<NowPipelineGraphic>(16);

        static readonly Comparison<NowPipelineGraphic> _orderComparison = CompareOrder;

        static int _nextRegistrationIndex;

        static bool _orderDirty = true;

        static int _lastPolledFrame = -1;

        static int _pollWatchStartFrame = -1;

        static bool _warnedMissingConsumer;

        [SerializeField] Camera _targetCamera;

        [SerializeField] bool _renderGameCameras = true;

        [SerializeField] bool _renderSceneView;

        [SerializeField] bool _renderPreviewCameras;

        [SerializeField] int _order;

        [SerializeField] NowGlassBlurQuality _glassBlurQuality = NowGlassBlurQuality.Auto;

        int _registrationIndex;

        int _scopeId;

        public event Action<NowPipelineGraphic, Camera, Rect> rebuildNowUI;

        public Camera targetCamera
        {
            get => _targetCamera;
            set => _targetCamera = value;
        }

        public bool renderGameCameras
        {
            get => _renderGameCameras;
            set => _renderGameCameras = value;
        }

        public bool renderSceneView
        {
            get => _renderSceneView;
            set => _renderSceneView = value;
        }

        public bool renderPreviewCameras
        {
            get => _renderPreviewCameras;
            set => _renderPreviewCameras = value;
        }

        public int order
        {
            get => _order;
            set
            {
                if (_order == value)
                    return;

                _order = value;
                _orderDirty = true;
            }
        }

        public NowGlassBlurQuality glassBlurQuality
        {
            get => _glassBlurQuality;
            set => _glassBlurQuality = value;
        }

        /// <summary>Explicit-rect pipeline hosts are one-pass; use NowPipelineLayoutGraphic for NowLayout content.</summary>
        internal virtual bool useLayoutMeasurePass => false;

        /// <summary>Resolves a SetId value within this host's private control scope.</summary>
        public int ResolveControlId(string id)
        {
            return NowControls.ResolveHostControlId(GetScopeId(), id);
        }

        public int ResolveControlId(int id)
        {
            return NowControls.ResolveHostControlId(GetScopeId(), id);
        }

        public static bool HasGraphicsFor(Camera camera)
        {
            _lastPolledFrame = Time.frameCount;

            if (camera == null)
                return false;

            for (int i = 0; i < _graphics.Count; ++i)
            {
                if (_graphics[i] != null && _graphics[i].CanRender(camera))
                    return true;
            }

            return false;
        }

        public static bool BuildDrawList(Camera camera, NowDrawList drawList)
        {
            return BuildDrawList(camera, drawList, 1f);
        }

        /// <summary>
        /// Builds the draw list in UI units of <paramref name="uiScale"/> pixels each,
        /// mirroring <see cref="Now.StartUI(float)"/> for pipeline rendering. Values
        /// at or below zero fall back to 1.
        /// </summary>
        public static bool BuildDrawList(Camera camera, NowDrawList drawList, float uiScale)
        {
            _lastPolledFrame = Time.frameCount;

            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            if (drawList == null)
                throw new ArgumentNullException(nameof(drawList));

            if (uiScale <= 0f || float.IsNaN(uiScale) || float.IsInfinity(uiScale))
                uiScale = 1f;

            var size = new Vector2(camera.pixelWidth / uiScale, camera.pixelHeight / uiScale);
            var frame = NowFrame.Begin(uiScale);
            NowDrawScope scope = default;

            try
            {
                scope = drawList.Begin(size);
                var surface = NowInputSurface.FromCamera(camera);
                surface.size /= uiScale;

                var inputScope = NowInput.Begin(NowInput.defaultProvider, surface);

                try
                {
                    SortGraphics();
                    var rect = new NowRect(0f, 0f, size.x, size.y);

                    for (int i = 0; i < _graphics.Count; ++i)
                    {
                        var graphic = _graphics[i];

                        if (graphic == null || !graphic.CanRender(camera))
                            continue;

                        using (NowControls.RestoreIdScope(graphic.GetScopeId()))
                        {
                            var content = new FrameContent(graphic, camera);
                            NowFrame.DrawContent(
                                ref content,
                                rect,
                                graphic.useLayoutMeasurePass,
                                trackContent: false,
                                flushOverlays: false);
                        }
                    }

                    NowOverlay.Flush();
                }
                catch
                {
                    // The pipeline capture must roll back before input disposal,
                    // otherwise failed content can flush deferred side effects.
                    scope.Cancel();
                    throw;
                }
                finally
                {
                    inputScope.Dispose();
                }

                scope.Dispose();
            }
            catch
            {
                scope.Cancel();
                throw;
            }
            finally
            {
                frame.Dispose();
            }

            return drawList.hasGeometry;
        }

        static void SortGraphics()
        {
            if (!_orderDirty)
                return;

            _graphics.Sort(_orderComparison);
            _orderDirty = false;
        }

        int GetScopeId()
        {
            if (_scopeId == 0)
                _scopeId = NowControls.AllocateHostScopeId();

            return _scopeId;
        }

        public bool CanRender(Camera camera)
        {
            if (camera == null || !isActiveAndEnabled)
                return false;

            if (_targetCamera != null)
                return _targetCamera == camera;

            switch (camera.cameraType)
            {
                case CameraType.Game:
                    return _renderGameCameras;
                case CameraType.SceneView:
                    return _renderSceneView;
                case CameraType.Preview:
                    return _renderPreviewCameras;
                default:
                    return false;
            }
        }

        protected virtual void OnEnable()
        {
            if (!_graphics.Contains(this))
            {
                _registrationIndex = ++_nextRegistrationIndex;
                _graphics.Add(this);
                _orderDirty = true;
            }

            _pollWatchStartFrame = Time.frameCount;
        }

        protected virtual void OnDisable()
        {
            if (_graphics.Remove(this))
                _orderDirty = true;
        }

        protected virtual void LateUpdate()
        {
            WarnIfNothingConsumesDrawLists();
        }

        /// <summary>
        /// A NowPipelineGraphic only renders when a render feature or custom pass
        /// polls <see cref="HasGraphicsFor"/> / <see cref="BuildDrawList"/>; without
        /// one it is silently invisible, so this warns once when enabled graphics
        /// have gone unpolled for a while.
        /// </summary>
        static void WarnIfNothingConsumesDrawLists()
        {
            if (_warnedMissingConsumer || !Application.isPlaying)
                return;

            int lastActivityFrame = Mathf.Max(_lastPolledFrame, _pollWatchStartFrame);

            if (Time.frameCount - lastActivityFrame <= MissingConsumerWarningFrames)
                return;

            _warnedMissingConsumer = true;
            Debug.LogWarning(BuildMissingConsumerMessage());
        }

        static string BuildMissingConsumerMessage()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;

            if (pipeline == null)
            {
                return "NowPipelineGraphic components are enabled but nothing consumes their draw lists: the Built-in Render Pipeline has no NowUI hook. Render them yourself by calling NowPipelineGraphic.BuildDrawList from a camera callback and drawing the result, or use NowGraphic / NowWorldGraphic instead.";
            }

            string pipelineName = pipeline.GetType().Name;

            if (pipelineName.Contains("Universal"))
            {
                return "NowPipelineGraphic components are enabled but nothing consumes their draw lists. Add the NowUniversalRendererFeature to the active Universal Renderer asset (Universal Render Pipeline Asset > Renderer > Add Renderer Feature).";
            }

            if (pipelineName.Contains("HDRenderPipeline") || pipelineName.Contains("HighDefinition"))
            {
                return "NowPipelineGraphic components are enabled but nothing consumes their draw lists. Add a Custom Pass Volume with a NowHighDefinitionCustomPass to the scene.";
            }

            return $"NowPipelineGraphic components are enabled but nothing consumes their draw lists on '{pipelineName}'. Add a render pass that calls NowPipelineGraphic.HasGraphicsFor / BuildDrawList.";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _orderDirty = true;
            _lastPolledFrame = -1;
            _pollWatchStartFrame = -1;
            _warnedMissingConsumer = false;
        }

        protected virtual void DrawNowUI(Camera camera, Rect rect)
        {
            rebuildNowUI?.Invoke(this, camera, rect);
        }

        struct FrameContent : INowFrameContent
        {
            readonly NowPipelineGraphic _owner;

            readonly Camera _camera;

            public FrameContent(NowPipelineGraphic owner, Camera camera)
            {
                _owner = owner;
                _camera = camera;
            }

            public void Draw(NowRect rect)
            {
                using (NowGlassSettings.PushBlurQuality(_owner._glassBlurQuality))
                    _owner.DrawNowUI(_camera, rect);
            }
        }

        static int CompareOrder(NowPipelineGraphic lhs, NowPipelineGraphic rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return 0;

            if (lhs == null)
                return 1;

            if (rhs == null)
                return -1;

            int order = lhs._order.CompareTo(rhs._order);
            return order != 0 ? order : lhs._registrationIndex.CompareTo(rhs._registrationIndex);
        }
    }

}
