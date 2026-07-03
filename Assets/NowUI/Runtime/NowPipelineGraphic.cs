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

        [SerializeField] bool _layoutMeasurePass = true;

        int _registrationIndex;

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

        /// <summary>
        /// When enabled, camera builds run a NowLayout measure pass (draws
        /// suppressed, input passive) before the real pass — so flexible space,
        /// stretching and auto-sized groups are exact every frame instead of
        /// settling one frame late. All graphics on a camera share one build,
        /// so the pass runs when any rendered graphic has it enabled; disable
        /// it on every graphic that skips NowLayout to save the extra pass.
        /// </summary>
        public bool layoutMeasurePass
        {
            get => _layoutMeasurePass;
            set => _layoutMeasurePass = value;
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
            var scope = drawList.Begin(size);

            try
            {
                var surface = NowInputSurface.FromCamera(camera);
                surface.size /= uiScale;

                using (NowInput.Begin(NowInput.defaultProvider, surface))
                {
                    var content = new FrameContent(camera);
                    NowFrame.DrawContent(
                        ref content,
                        new NowRect(0f, 0f, size.x, size.y),
                        AnyRenderableWantsMeasurePass(camera),
                        trackContent: false);
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

        static bool AnyRenderableWantsMeasurePass(Camera camera)
        {
            for (int i = 0; i < _graphics.Count; ++i)
            {
                var graphic = _graphics[i];

                if (graphic != null && graphic._layoutMeasurePass && graphic.CanRender(camera))
                    return true;
            }

            return false;
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
            readonly Camera _camera;

            public FrameContent(Camera camera)
            {
                _camera = camera;
            }

            public void Draw(NowRect rect)
            {
                DrawAll(_camera, rect);
            }
        }

        static void DrawAll(Camera camera, Rect rect)
        {
            if (_orderDirty)
            {
                _graphics.Sort(_orderComparison);
                _orderDirty = false;
            }

            for (int i = 0; i < _graphics.Count; ++i)
            {
                var graphic = _graphics[i];

                if (graphic == null || !graphic.CanRender(camera))
                    continue;

                using (NowGlassSettings.PushBlurQuality(graphic._glassBlurQuality))
                    graphic.DrawNowUI(camera, rect);
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
