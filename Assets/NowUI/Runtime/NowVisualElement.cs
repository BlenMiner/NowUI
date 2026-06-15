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

        IVisualElementScheduledItem _repaintItem;

        NowRenderer _renderer;

        RenderTexture _target;

        bool _rebuildEveryFrame;

        bool _autoRebuildOnInteraction = true;

        bool _layoutMeasurePass = true;

        bool _usePanelScale = true;

        float _uiScale = 1f;

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

        [UxmlAttribute]
        public bool autoRebuildOnInteraction
        {
            get => _autoRebuildOnInteraction;
            set => _autoRebuildOnInteraction = value;
        }

        [UxmlAttribute]
        public bool layoutMeasurePass
        {
            get => _layoutMeasurePass;
            set
            {
                if (_layoutMeasurePass == value)
                    return;

                _layoutMeasurePass = value;
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
            float previousScale = Now.uiScale;
            bool tracking = false;
            bool contentTracking = false;
            var scope = renderer.Begin(size);

            try
            {
                Now.SetUIScale(GetEffectiveUIScale(pixelsPerPoint));
                NowControlState.BeginRepaintTracking();
                tracking = true;

                using (NowInput.Begin(_inputProvider, new NowInputSurface(size)))
                {
                    if (_layoutMeasurePass)
                    {
                        using (NowProfiler.MeasurePass.Auto())
                        {
                            int layoutCounter = NowLayout.BeginMeasurePass();

                            try
                            {
                                DrawNowUI(nowRect);
                            }
                            finally
                            {
                                NowLayout.EndMeasurePass(layoutCounter);
                            }
                        }
                    }

                    using (NowProfiler.Draw.Auto())
                    {
                        NowLayout.BeginContentTracking();
                        contentTracking = true;
                        DrawNowUI(nowRect);
                        _measuredContentSize = NowLayout.EndContentTracking();
                        contentTracking = false;
                    }

                    NowOverlay.Flush();
                }

                _wantsInteractionRepaint = NowControlState.EndRepaintTracking();
                tracking = false;

                scope.Dispose();
                renderer.Render(target, true, _clearColor);
            }
            catch (Exception ex)
            {
                if (contentTracking)
                    NowLayout.EndContentTracking();

                if (tracking)
                    NowControlState.EndRepaintTracking();

                _wantsInteractionRepaint = false;
                scope.Cancel();
                renderer.Clear();
                Debug.LogException(ex);
            }
            finally
            {
                Now.SetUIScale(previousScale);
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
            if (_inputProvider.KeyDown(evt.keyCode))
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
    /// Event-buffered input provider used by <see cref="NowVisualElement"/> and
    /// available for tests/custom UI Toolkit hosts.
    /// </summary>
    public sealed class NowUIToolkitInputProvider : INowInputProvider
    {
        bool _hasPointer;

        Vector2 _pointerPosition;

        Vector2 _previousPointerPosition;

        NowPointerButtons _pointerButtonsDown;

        NowPointerButtons _pointerButtonsPressed;

        NowPointerButtons _pointerButtonsReleased;

        Vector2 _scrollDelta;

        Vector2 _navigation;

        bool _navigationTransient;

        bool _submitDown;

        bool _submitPressed;

        bool _submitReleased;

        bool _cancelDown;

        bool _cancelPressed;

        bool _cancelReleased;

        bool _leftDown;

        bool _rightDown;

        bool _upDown;

        bool _downDown;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            Vector2 delta = _hasPointer ? _pointerPosition - _previousPointerPosition : default;

            snapshot = new NowInputSnapshot(
                _hasPointer,
                _pointerPosition,
                _previousPointerPosition,
                delta,
                _pointerButtonsDown,
                _pointerButtonsPressed,
                _pointerButtonsReleased,
                _scrollDelta,
                _navigation,
                _submitDown,
                _submitPressed,
                _submitReleased,
                _cancelDown,
                _cancelPressed,
                _cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);

            ClearTransient(snapshot);
            return true;
        }

        public void SetPointerPosition(Vector2 position)
        {
            SetPointerPosition(position, (int)_pointerButtonsDown);
        }

        public void SetPointerPosition(Vector2 position, int pressedButtons)
        {
            if (!_hasPointer)
                _previousPointerPosition = position;

            _hasPointer = true;
            _pointerPosition = position;
            _pointerButtonsDown = ToButtonMask(pressedButtons);
        }

        public void SetPointerDown(Vector2 position, int button, int pressedButtons)
        {
            SetPointerPosition(position, pressedButtons);

            if (TryGetButton(button, out var pointerButton))
            {
                var mask = NowInputSnapshot.ToButtonMask(pointerButton);
                _pointerButtonsDown |= mask;
                _pointerButtonsPressed |= mask;
            }
        }

        public void SetPointerUp(Vector2 position, int button, int pressedButtons)
        {
            SetPointerPosition(position, pressedButtons);

            if (TryGetButton(button, out var pointerButton))
            {
                var mask = NowInputSnapshot.ToButtonMask(pointerButton);
                _pointerButtonsDown &= ~mask;
                _pointerButtonsReleased |= mask;
            }
        }

        public void CancelPointer()
        {
            _pointerButtonsReleased |= _pointerButtonsDown;
            _pointerButtonsDown = NowPointerButtons.None;
            _hasPointer = false;
        }

        public void ClearPointer()
        {
            _hasPointer = false;
        }

        public void AddScrollDelta(Vector2 delta)
        {
            _scrollDelta += delta;
        }

        public void SetNavigation(Vector2 navigation)
        {
            _navigation = Vector2.ClampMagnitude(navigation, 1f);
            _navigationTransient = true;
        }

        public void PressSubmit()
        {
            _submitDown = true;
            _submitPressed = true;
            _submitReleased = true;
        }

        public void PressCancel()
        {
            _cancelDown = true;
            _cancelPressed = true;
            _cancelReleased = true;
        }

        public bool KeyDown(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    _leftDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    _rightDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    _upDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    _downDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    _submitDown = true;
                    _submitPressed = true;
                    return true;
                case KeyCode.Escape:
                    _cancelDown = true;
                    _cancelPressed = true;
                    return true;
                default:
                    return false;
            }
        }

        public bool KeyUp(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    _leftDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    _rightDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    _upDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    _downDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    _submitDown = false;
                    _submitReleased = true;
                    return true;
                case KeyCode.Escape:
                    _cancelDown = false;
                    _cancelReleased = true;
                    return true;
                default:
                    return false;
            }
        }

        public void Reset()
        {
            _hasPointer = false;
            _pointerPosition = default;
            _previousPointerPosition = default;
            _pointerButtonsDown = NowPointerButtons.None;
            _pointerButtonsPressed = NowPointerButtons.None;
            _pointerButtonsReleased = NowPointerButtons.None;
            _scrollDelta = default;
            _navigation = default;
            _navigationTransient = false;
            _submitDown = false;
            _submitPressed = false;
            _submitReleased = false;
            _cancelDown = false;
            _cancelPressed = false;
            _cancelReleased = false;
            _leftDown = false;
            _rightDown = false;
            _upDown = false;
            _downDown = false;
        }

        void ClearTransient(NowInputSnapshot snapshot)
        {
            _previousPointerPosition = snapshot.pointerPosition;
            _pointerButtonsPressed = NowPointerButtons.None;
            _pointerButtonsReleased = NowPointerButtons.None;
            _scrollDelta = default;
            _submitPressed = false;
            _cancelPressed = false;

            if (_submitReleased)
            {
                _submitDown = false;
                _submitReleased = false;
            }

            if (_cancelReleased)
            {
                _cancelDown = false;
                _cancelReleased = false;
            }

            if (_navigationTransient)
            {
                _navigation = default;
                _navigationTransient = false;
            }
        }

        void UpdateKeyNavigation()
        {
            float x = 0f;
            float y = 0f;

            if (_leftDown)
                x -= 1f;

            if (_rightDown)
                x += 1f;

            if (_downDown)
                y -= 1f;

            if (_upDown)
                y += 1f;

            _navigation = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            _navigationTransient = false;
        }

        static NowPointerButtons ToButtonMask(int pressedButtons)
        {
            NowPointerButtons buttons = NowPointerButtons.None;

            if ((pressedButtons & 1) != 0)
                buttons |= NowPointerButtons.Primary;

            if ((pressedButtons & 2) != 0)
                buttons |= NowPointerButtons.Secondary;

            if ((pressedButtons & 4) != 0)
                buttons |= NowPointerButtons.Middle;

            if ((pressedButtons & 8) != 0)
                buttons |= NowPointerButtons.Back;

            if ((pressedButtons & 16) != 0)
                buttons |= NowPointerButtons.Forward;

            return buttons;
        }

        static bool TryGetButton(int button, out NowPointerButton pointerButton)
        {
            switch (button)
            {
                case 0:
                    pointerButton = NowPointerButton.Primary;
                    return true;
                case 1:
                    pointerButton = NowPointerButton.Secondary;
                    return true;
                case 2:
                    pointerButton = NowPointerButton.Middle;
                    return true;
                case 3:
                    pointerButton = NowPointerButton.Back;
                    return true;
                case 4:
                    pointerButton = NowPointerButton.Forward;
                    return true;
                default:
                    pointerButton = default;
                    return false;
            }
        }
    }
}
