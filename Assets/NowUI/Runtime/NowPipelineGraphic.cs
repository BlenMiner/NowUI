using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    [AddComponentMenu("NowUI/Now Pipeline Graphic")]
    [ExecuteAlways]
    public class NowPipelineGraphic : MonoBehaviour
    {
        static readonly List<NowPipelineGraphic> _graphics = new List<NowPipelineGraphic>(16);

        static readonly Comparison<NowPipelineGraphic> _orderComparison = CompareOrder;

        static int _nextRegistrationIndex;

        [SerializeField] Camera _targetCamera;

        [SerializeField] bool _renderGameCameras = true;

        [SerializeField] bool _renderSceneView;

        [SerializeField] bool _renderPreviewCameras;

        [SerializeField] int _order;

        [SerializeField] NowGlassBlurQuality _glassBlurQuality = NowGlassBlurQuality.Auto;

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
            set => _order = value;
        }

        public NowGlassBlurQuality glassBlurQuality
        {
            get => _glassBlurQuality;
            set => _glassBlurQuality = value;
        }

        public static bool HasGraphicsFor(Camera camera)
        {
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
                        measurePass: false,
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
            }
        }

        protected virtual void OnDisable()
        {
            _graphics.Remove(this);
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
            _graphics.Sort(_orderComparison);

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
