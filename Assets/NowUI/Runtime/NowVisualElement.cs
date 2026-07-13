using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using UIEVertex = UnityEngine.UIElements.Vertex;

namespace NowUI
{
    /// <summary>
    /// UI Toolkit host for immediate NowUI drawing. Place this element in UXML/UI
    /// Builder, then either subscribe to <see cref="rebuildNowUI"/> or derive from
    /// it and override <see cref="DrawNowUI"/>.
    /// </summary>
    [UxmlElement]
    public partial class NowVisualElement : VisualElement, IDisposable
    {
        static readonly ushort[] s_indices = { 0, 1, 2, 2, 3, 0 };

        static readonly UIEVertex[] s_vertices = new UIEVertex[4];

        readonly NowUIToolkitInputProvider _inputProvider = new NowUIToolkitInputProvider();

        readonly int _scopeId = NowControls.AllocateHostScopeId();

        IVisualElementScheduledItem _repaintItem;

        NowRenderer _renderer;

        RenderTexture _target;

        bool _rebuildEveryFrame;

        bool _autoRebuildOnInteraction = true;

        bool _usePanelScale = true;

        float _uiScale = 1f;

        NowGlassBlurQuality _glassBlurQuality = NowGlassBlurQuality.Auto;

        Color _clearColor = Color.clear;

        bool _wantsInteractionRepaint;

        Vector2 _measuredContentSize;

        bool _disposed;

        static NowVisualElement()
        {
            for (int i = 0; i < s_vertices.Length; ++i)
                s_vertices[i].tint = Color.white;

            s_vertices[0].uv = new Vector2(0f, 0f);
            s_vertices[1].uv = new Vector2(0f, 1f);
            s_vertices[2].uv = new Vector2(1f, 1f);
            s_vertices[3].uv = new Vector2(1f, 0f);
        }

        public NowVisualElement()
        {
            pickingMode = PickingMode.Position;
            focusable = true;
            tabIndex = 0;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<NavigationMoveEvent>(OnNavigationMove);
            RegisterCallback<NavigationSubmitEvent>(OnNavigationSubmit);
            RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp);

            _repaintItem = schedule.Execute(ScheduledRepaint).Every(16);
            _repaintItem.Pause();
        }

        public event Action<NowVisualElement, NowRect> rebuildNowUI;

        [UxmlAttribute]
        public bool rebuildEveryFrame
        {
            get => _rebuildEveryFrame;
            set
            {
                if (_rebuildEveryFrame == value)
                    return;

                _rebuildEveryFrame = value;
                MarkDirty();
            }
        }

        /// <summary>Resolves a SetId value within this element's private control scope.</summary>
        public int ResolveControlId(string id)
        {
            return NowControls.ResolveHostControlId(_scopeId, id);
        }

        public int ResolveControlId(int id)
        {
            return NowControls.ResolveHostControlId(_scopeId, id);
        }

        [UxmlAttribute]
        public bool autoRebuildOnInteraction
        {
            get => _autoRebuildOnInteraction;
            set => _autoRebuildOnInteraction = value;
        }

        /// <summary>Explicit-rect UI Toolkit hosts are one-pass; use NowLayoutVisualElement for NowLayout content.</summary>
        internal virtual bool useLayoutMeasurePass => false;

        [UxmlAttribute]
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

        /// <summary>
        /// When true, NowUI's pixel scale follows the owning panel's
        /// scaled-pixels-per-point so text, effects, and RenderTexture allocation
        /// stay crisp under PanelSettings scaling.
        /// </summary>
        [UxmlAttribute]
        public bool usePanelScale
        {
            get => _usePanelScale;
            set
            {
                if (_usePanelScale == value)
                    return;

                _usePanelScale = value;
                MarkDirty();
            }
        }

        /// <summary>Additional multiplier applied on top of the panel scale.</summary>
        [UxmlAttribute]
        public float uiScale
        {
            get => _uiScale;
            set
            {
                float next = SanitizeScale(value);

                if (Mathf.Approximately(_uiScale, next))
                    return;

                _uiScale = next;
                MarkDirty();
            }
        }

        [UxmlAttribute]
        public Color clearColor
        {
            get => _clearColor;
            set
            {
                if (_clearColor == value)
                    return;

                _clearColor = value;
                MarkDirty();
            }
        }

        public RenderTexture targetTexture => _target;

        public Vector2 measuredContentSize => _measuredContentSize;

        public void MarkDirty()
        {
            MarkDirtyRepaint();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _repaintItem?.Pause();
            ReleaseTarget();

            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }

        protected virtual void DrawNowUI(NowRect rect)
        {
            rebuildNowUI?.Invoke(this, rect);
        }

        struct FrameContent : INowFrameContent
        {
            readonly NowVisualElement _owner;

            public FrameContent(NowVisualElement owner)
            {
                _owner = owner;
            }

            public void Draw(NowRect rect)
            {
                _owner.DrawNowUI(rect);
            }
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            _disposed = false;
            _inputProvider.Reset();
            _repaintItem?.Resume();
            MarkDirty();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            _inputProvider.Reset();
            Dispose();
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if ((evt.oldRect.size - evt.newRect.size).sqrMagnitude > 0.25f)
                MarkDirty();
        }

        void ScheduledRepaint()
        {
            if (panel == null)
                return;

            if (_rebuildEveryFrame || (_autoRebuildOnInteraction && _wantsInteractionRepaint))
                MarkDirty();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var rect = contentRect;

            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float pixelsPerPoint = GetPixelsPerPoint();
            int pixelWidth = Mathf.Max(1, Mathf.CeilToInt(rect.width * pixelsPerPoint));
            int pixelHeight = Mathf.Max(1, Mathf.CeilToInt(rect.height * pixelsPerPoint));
            var target = GetTarget(pixelWidth, pixelHeight);

            RebuildTarget(rect, target, pixelsPerPoint);
            DrawTarget(context, rect, target);
        }

        void RebuildTarget(Rect rect, RenderTexture target, float pixelsPerPoint)
        {
            var renderer = GetRenderer();
            var size = new Vector2(rect.width, rect.height);
            var nowRect = new NowRect(0f, 0f, size.x, size.y);
            renderer.glassBlurQuality = _glassBlurQuality;
            var frame = NowFrame.Begin(GetEffectiveUIScale(pixelsPerPoint), trackRepaint: true);
            NowDrawScope scope = default;

            try
            {
                scope = renderer.Begin(size);
                var inputScope = NowInput.Begin(_inputProvider, new NowInputSurface(size));

                try
                {
                    using (NowControls.RestoreIdScope(_scopeId))
                    {
                        var content = new FrameContent(this);
                        _measuredContentSize = NowFrame.DrawContent(
                            ref content,
                            nowRect,
                            useLayoutMeasurePass,
                            trackContent: true);
                    }
                }
                catch
                {
                    // Prevent input finalization from flushing overlays queued by
                    // a retained rebuild that is about to be discarded.
                    scope.Cancel();
                    throw;
                }
                finally
                {
                    inputScope.Dispose();
                }

                _wantsInteractionRepaint = frame.EndRepaintTracking();

                scope.Dispose();
                renderer.Render(target, true, _clearColor);
            }
            catch (Exception ex)
            {
                _wantsInteractionRepaint = false;
                scope.Cancel();
                renderer.Clear();
                Debug.LogException(ex);
            }
            finally
            {
                frame.Dispose();
            }
        }

        RenderTexture GetTarget(int width, int height)
        {
            if (_target != null && _target.width == width && _target.height == height)
                return _target;

            ReleaseTarget();

            _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Now UI Toolkit",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            _target.Create();
            return _target;
        }

        NowRenderer GetRenderer()
        {
            return _renderer ??= new NowRenderer();
        }

        void ReleaseTarget()
        {
            if (_target == null)
                return;

            _target.Release();

            if (Application.isPlaying)
                Object.Destroy(_target);
            else
                Object.DestroyImmediate(_target);

            _target = null;
        }

        float GetPixelsPerPoint()
        {
            float value = _usePanelScale && panel != null ? scaledPixelsPerPoint : 1f;
            return SanitizeScale(value);
        }

        float GetEffectiveUIScale(float pixelsPerPoint)
        {
            return SanitizeScale((_usePanelScale ? pixelsPerPoint : 1f) * _uiScale);
        }

        static float SanitizeScale(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 1f;
        }

        static void DrawTarget(MeshGenerationContext context, Rect rect, Texture texture)
        {
            float left = rect.xMin;
            float right = rect.xMax;
            float top = rect.yMin;
            float bottom = rect.yMax;

            s_vertices[0].position = new Vector3(left, bottom, UIEVertex.nearZ);
            s_vertices[1].position = new Vector3(left, top, UIEVertex.nearZ);
            s_vertices[2].position = new Vector3(right, top, UIEVertex.nearZ);
            s_vertices[3].position = new Vector3(right, bottom, UIEVertex.nearZ);

            var mesh = context.Allocate(s_vertices.Length, s_indices.Length, texture);
            mesh.SetAllVertices(s_vertices);
            mesh.SetAllIndices(s_indices);
        }

        void OnPointerEnter(PointerEnterEvent evt)
        {
            _inputProvider.SetPointerPosition(evt.localPosition);
            MarkInteractionDirty(evt);
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            _inputProvider.SetPointerPosition(evt.localPosition, evt.pressedButtons);
            MarkInteractionDirty(evt);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            Focus();

            if (!PointerCaptureHelper.HasPointerCapture(this, evt.pointerId))
                PointerCaptureHelper.CapturePointer(this, evt.pointerId);

            _inputProvider.SetPointerDown(evt.localPosition, evt.button, evt.pressedButtons);
            MarkInteractionDirty(evt);
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            _inputProvider.SetPointerUp(evt.localPosition, evt.button, evt.pressedButtons);

            if (PointerCaptureHelper.HasPointerCapture(this, evt.pointerId))
                PointerCaptureHelper.ReleasePointer(this, evt.pointerId);

            MarkInteractionDirty(evt);
        }

        void OnPointerCancel(PointerCancelEvent evt)
        {
            _inputProvider.CancelPointer();

            if (PointerCaptureHelper.HasPointerCapture(this, evt.pointerId))
                PointerCaptureHelper.ReleasePointer(this, evt.pointerId);

            MarkInteractionDirty(evt);
        }

        void OnPointerLeave(PointerLeaveEvent evt)
        {
            _inputProvider.SetPointerPosition(evt.localPosition, evt.pressedButtons);

            if (evt.pressedButtons == 0)
                _inputProvider.ClearPointer();

            MarkInteractionDirty(evt);
        }

        void OnWheel(WheelEvent evt)
        {
            _inputProvider.AddScrollDelta(evt.delta);
            MarkInteractionDirty(evt);
        }

        void OnNavigationMove(NavigationMoveEvent evt)
        {
            _inputProvider.SetNavigation(evt.move);
            MarkInteractionDirty(evt);
        }

        void OnNavigationSubmit(NavigationSubmitEvent evt)
        {
            _inputProvider.PressSubmit();
            MarkInteractionDirty(evt);
        }

        void OnNavigationCancel(NavigationCancelEvent evt)
        {
            _inputProvider.PressCancel();
            MarkInteractionDirty(evt);
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (_inputProvider.KeyDown(evt.keyCode, evt.shiftKey))
                MarkInteractionDirty(evt);
        }

        void OnKeyUp(KeyUpEvent evt)
        {
            if (_inputProvider.KeyUp(evt.keyCode))
                MarkInteractionDirty(evt);
        }

        void MarkInteractionDirty(EventBase evt)
        {
            if (_autoRebuildOnInteraction)
                MarkDirty();

            evt.StopPropagation();
        }
    }

    /// <summary>UI Toolkit host with exact same-rebuild NowLayout measurement enabled.</summary>
    [UxmlElement]
    public partial class NowLayoutVisualElement : NowVisualElement
    {
        internal sealed override bool useLayoutMeasurePass => true;
    }
}
