#if NOWUI_UITOOLKIT
using System;
using System.Reflection;
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
    public partial class NowVisualElement : VisualElement, IDisposable, INowDynamicTextureHost
    {
        static readonly ushort[] s_indices = { 0, 1, 2, 2, 3, 0 };

        static readonly UIEVertex[] s_vertices = new UIEVertex[4];

        readonly NowUIToolkitInputProvider _inputProvider = new NowUIToolkitInputProvider();

        readonly NowResolvedId _scopeId = NowControls.AllocateOwnerScope();

        const long ContinuousRepaintIntervalMilliseconds = 16;

        IVisualElementScheduledItem _continuousRepaintItem;

        IVisualElementScheduledItem _interactionRepaintItem;

        NowRenderer _renderer;

        RenderTexture _target;

        bool _rebuildEveryFrame;

        bool _autoRebuildOnInteraction = true;

        bool _usePanelScale = true;

        float _uiScale = 1f;

        NowGlassBlurQuality _glassBlurQuality = NowGlassBlurQuality.Auto;

        Color _clearColor = Color.clear;

        NowInteractionRepaintTracker _interactionRepaintTracker;

        float _nextInteractionRepaintAt = float.PositiveInfinity;

        Vector2 _measuredContentSize;

        bool _disposed;
        int _dynamicTextureBuildVersion;

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

            _continuousRepaintItem = schedule
                .Execute(ScheduledContinuousRepaint)
                .Every(ContinuousRepaintIntervalMilliseconds);
            _continuousRepaintItem.Pause();

            _interactionRepaintItem = schedule.Execute(ScheduledInteractionRepaint);
            _interactionRepaintItem.Pause();
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
                RefreshContinuousRepaintSchedule();
                RefreshInteractionRepaintSchedule();
                MarkDirty();
            }
        }

        /// <summary>Resolves a SetId value within this element's private control scope.</summary>
        public NowResolvedId ResolveControlId(string id)
        {
            return _scopeId.Derive(NowIdDomain.Control, id);
        }

        public NowResolvedId ResolveControlId(int id)
        {
            return _scopeId.Derive(NowIdDomain.Control, id);
        }

        [UxmlAttribute]
        public bool autoRebuildOnInteraction
        {
            get => _autoRebuildOnInteraction;
            set
            {
                if (_autoRebuildOnInteraction == value)
                    return;

                _autoRebuildOnInteraction = value;
                RefreshInteractionRepaintSchedule();
            }
        }

        internal bool interactionRepaintDue => _interactionRepaintTracker.wantsRepaint;

        internal float nextInteractionRepaintAt => _nextInteractionRepaintAt;

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

        public virtual void MarkDirty()
        {
            MarkDirtyRepaint();
        }

        int INowDynamicTextureHost.dynamicTextureBuildVersion => _dynamicTextureBuildVersion;

        bool INowDynamicTextureHost.isDynamicTextureHostValid => !_disposed && panel != null;

        void INowDynamicTextureHost.BeginDynamicTextureBuild()
        {
            unchecked
            {
                ++_dynamicTextureBuildVersion;

                if (_dynamicTextureBuildVersion == 0)
                    ++_dynamicTextureBuildVersion;
            }
        }

        void INowDynamicTextureHost.RequestDynamicTextureRebuild()
        {
            MarkDirty();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _continuousRepaintItem?.Pause();
            ClearInteractionRepaintRequest();
            _inputProvider.Reset();
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
            ClearInteractionRepaintRequest();
            RefreshContinuousRepaintSchedule();
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

        void ScheduledContinuousRepaint()
        {
            if (panel == null || !_rebuildEveryFrame)
                return;

            MarkDirty();
        }

        void ScheduledInteractionRepaint()
        {
            if (_disposed || panel == null || !_autoRebuildOnInteraction || _rebuildEveryFrame)
                return;

            if (!_interactionRepaintTracker.wantsRepaint)
            {
                // The scheduler may wake a millisecond early. Retain the
                // original absolute deadline and wait only for the remainder.
                RefreshInteractionRepaintSchedule();
                return;
            }

            _interactionRepaintItem?.Pause();
            MarkDirty();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var rect = contentRect;

            if (rect.width <= 0f || rect.height <= 0f)
            {
                NowOverlay.ReleaseRegistrationOwner(_inputProvider);
                return;
            }

            float pixelsPerPoint = GetPixelsPerPoint();
            int pixelWidth = Mathf.Max(1, Mathf.CeilToInt(rect.width * pixelsPerPoint));
            int pixelHeight = Mathf.Max(1, Mathf.CeilToInt(rect.height * pixelsPerPoint));
            var target = GetTarget(pixelWidth, pixelHeight);

            RebuildTarget(rect, target, pixelsPerPoint);
            DrawTarget(context, rect, target);
        }

        /// <summary>
        /// Runs the content once as a measure-only pass inside
        /// <paramref name="availableSize"/> and returns the extents NowLayout
        /// tracked. Nothing is drawn and input stays passive.
        /// </summary>
        internal Vector2 MeasureLayoutContent(Vector2 availableSize)
        {
            var size = new Vector2(Mathf.Max(0f, availableSize.x), Mathf.Max(0f, availableSize.y));
            var frame = NowFrame.Begin(GetEffectiveUIScale(GetPixelsPerPoint()));

            try
            {
                using (NowInput.BeginMeasurement(_inputProvider, new NowInputSurface(size)))
                using (NowControls.RestoreIdScope(_scopeId))
                {
                    var content = new FrameContent(this);
                    return NowFrame.MeasureContent(ref content, new NowRect(0f, 0f, size.x, size.y));
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return default;
            }
            finally
            {
                frame.Dispose();
            }
        }

        void RebuildTarget(Rect rect, RenderTexture target, float pixelsPerPoint)
        {
            var renderer = GetRenderer();
            var size = new Vector2(rect.width, rect.height);
            var nowRect = new NowRect(0f, 0f, size.x, size.y);
            renderer.glassBlurQuality = _glassBlurQuality;
            var frame = NowFrame.Begin(
                GetEffectiveUIScale(pixelsPerPoint),
                trackRepaint: true,
                dynamicTextureHost: this);
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

                bool wantsRepaint = frame.EndRepaintTracking(out float nextRepaintAt);
                SetInteractionRepaintRequest(wantsRepaint, nextRepaintAt);

                scope.Dispose();
                renderer.Render(target, true, _clearColor);
            }
            catch (Exception ex)
            {
                ClearInteractionRepaintRequest();
                scope.Cancel();
                renderer.Clear();
                Debug.LogException(ex);
            }
            finally
            {
                frame.Dispose();
            }
        }

        internal void SetInteractionRepaintRequest(bool immediate, float nextRepaintAt)
        {
            _interactionRepaintTracker.SetRepaintRequest(immediate, nextRepaintAt);
            _nextInteractionRepaintAt = IsFinite(nextRepaintAt)
                ? nextRepaintAt
                : float.PositiveInfinity;
            RefreshInteractionRepaintSchedule();
        }

        void ClearInteractionRepaintRequest()
        {
            _interactionRepaintTracker.Reset();
            _nextInteractionRepaintAt = float.PositiveInfinity;
            _interactionRepaintItem?.Pause();
        }

        void RefreshContinuousRepaintSchedule()
        {
            if (_continuousRepaintItem == null)
                return;

            if (!_disposed && panel != null && _rebuildEveryFrame)
                _continuousRepaintItem.Resume();
            else
                _continuousRepaintItem.Pause();
        }

        void RefreshInteractionRepaintSchedule()
        {
            if (_interactionRepaintItem == null)
                return;

            long delay = InteractionRepaintDelayMilliseconds(
                _interactionRepaintTracker.wantsRepaint,
                _nextInteractionRepaintAt,
                Time.realtimeSinceStartup);

            if (_disposed ||
                panel == null ||
                !_autoRebuildOnInteraction ||
                _rebuildEveryFrame ||
                delay < 0)
            {
                _interactionRepaintItem.Pause();
                return;
            }

            _interactionRepaintItem.Pause();
            _interactionRepaintItem.ExecuteLater(delay);
        }

        internal static long InteractionRepaintDelayMilliseconds(
            bool immediate,
            float nextRepaintAt,
            float realtime)
        {
            if (immediate)
                return 0;

            if (!IsFinite(nextRepaintAt))
                return -1;

            double milliseconds = Math.Ceiling(
                Math.Max(0d, ((double)nextRepaintAt - realtime) * 1000d));

            // UI Toolkit's scheduler accepts a long, but its platform timer
            // need not. A very distant deadline can wake and reschedule safely.
            return (long)Math.Min(milliseconds, int.MaxValue);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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

    /// <summary>
    /// UI Toolkit host with exact same-rebuild NowLayout measurement enabled.
    /// It also reports its layout content size to UI Toolkit, so an element
    /// whose width or height is <c>auto</c> shrink-wraps its NowLayout content
    /// the way a <see cref="Label"/> wraps its text. Call <see cref="MarkDirty"/>
    /// when retained data changes so UI Toolkit measures the new content.
    /// </summary>
    [UxmlElement]
    public partial class NowLayoutVisualElement : NowVisualElement
    {
        public NowLayoutVisualElement()
        {
            NowUIToolkitMeasureBridge.EnableMeasureFunction(this);
        }

        internal sealed override bool useLayoutMeasurePass => true;

        /// <summary>
        /// True when UI Toolkit asks this element for its content size. It is
        /// false only if this Unity version hides the hook, in which case the
        /// element needs an explicit size or flex settings like any other
        /// <see cref="VisualElement"/>.
        /// </summary>
        public bool reportsContentSize => NowUIToolkitMeasureBridge.available;

        public override void MarkDirty()
        {
            base.MarkDirty();
            NowUIToolkitMeasureBridge.InvalidateLayout(this);
        }

        protected override Vector2 DoMeasure(
            float desiredWidth,
            MeasureMode widthMode,
            float desiredHeight,
            MeasureMode heightMode)
        {
            return MeasureContentSize(desiredWidth, widthMode, desiredHeight, heightMode);
        }

        /// <summary>
        /// Measures the NowLayout content for UI Toolkit. A constrained axis is
        /// measured inside the offered size; an unconstrained (<c>auto</c>)
        /// axis is measured at zero so fill and grow children contribute
        /// nothing and the result is the content's own preferred extent.
        /// </summary>
        internal Vector2 MeasureContentSize(
            float desiredWidth,
            MeasureMode widthMode,
            float desiredHeight,
            MeasureMode heightMode)
        {
            var available = new Vector2(
                widthMode == MeasureMode.Undefined ? 0f : Sanitize(desiredWidth),
                heightMode == MeasureMode.Undefined ? 0f : Sanitize(desiredHeight));
            Vector2 measured = MeasureLayoutContent(available);

            return new Vector2(
                Resolve(measured.x, desiredWidth, widthMode),
                Resolve(measured.y, desiredHeight, heightMode));
        }

        static float Resolve(float measured, float desired, MeasureMode mode)
        {
            measured = Sanitize(measured);
            switch (mode)
            {
                case MeasureMode.Exactly:
                    return Sanitize(desired);
                case MeasureMode.AtMost:
                    return Mathf.Min(measured, Sanitize(desired));
                default:
                    return measured;
            }
        }

        static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }
    }

    /// <summary>
    /// UI Toolkit only calls <c>DoMeasure</c> for elements that opted in through
    /// an internal flag, and only re-measures after an internal layout version
    /// bump; its own Label and Image use the same two members. This bridge
    /// reaches them by reflection and degrades to a one-time warning if a
    /// Unity release renames them.
    /// </summary>
    static class NowUIToolkitMeasureBridge
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static readonly PropertyInfo _requireMeasureFunction;
        static readonly MethodInfo _incrementVersion;
        static readonly object[] _layoutChangeArgs;
        static bool _warned;

        static NowUIToolkitMeasureBridge()
        {
            _requireMeasureFunction = typeof(VisualElement).GetProperty("requireMeasureFunction", Flags);
            _incrementVersion = ResolveIncrementVersion(out object layoutChange);
            _layoutChangeArgs = layoutChange != null ? new[] { layoutChange } : null;
        }

        internal static bool available => _requireMeasureFunction != null && _requireMeasureFunction.CanWrite;

        internal static void EnableMeasureFunction(VisualElement element)
        {
            if (available)
            {
                _requireMeasureFunction.SetValue(element, true);
                return;
            }

            if (_warned)
                return;

            _warned = true;
            Debug.LogWarning(
                "NowLayoutVisualElement cannot report its content size to UI Toolkit in this Unity version. " +
                "Give the element an explicit width and height, or flex-grow inside a sized parent.");
        }

        internal static void InvalidateLayout(VisualElement element)
        {
            if (_incrementVersion == null || _layoutChangeArgs == null || element.panel == null)
                return;

            _incrementVersion.Invoke(element, _layoutChangeArgs);
        }

        static MethodInfo ResolveIncrementVersion(out object layoutChange)
        {
            layoutChange = null;
            Type changeType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.VersionChangeType");
            if (changeType == null || !changeType.IsEnum || !Enum.IsDefined(changeType, "Layout"))
                return null;

            MethodInfo method = typeof(VisualElement).GetMethod("IncrementVersion", Flags, null, new[] { changeType }, null);
            if (method == null)
                return null;

            layoutChange = Enum.Parse(changeType, "Layout");
            return method;
        }
    }
}
#endif
