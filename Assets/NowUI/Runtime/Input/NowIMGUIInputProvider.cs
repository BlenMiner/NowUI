using System;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Native IMGUI event adapter. Host-backed instances route pointer events
    /// through one IMGUI control id so Unity can preserve drag ownership outside
    /// the panel and report capture loss without leaving NowUI controls active.
    /// </summary>
    public sealed class NowIMGUIInputProvider : INowInputProvider
    {
        public static readonly NowIMGUIInputProvider instance = new NowIMGUIInputProvider();

        internal static Action repaintRequested;

        internal static Action<NowIMGUIInputProvider> hostRepaintRequested;

        internal static Action<NowIMGUIInputProvider, float> hostRepaintAfterRequested;

        static int s_inputPass;

        readonly int _hostControlId;

        readonly object _hostContext;

        Event _sampledEvent;

        EventType _sampledType;

        NowPointerButtons _buttonsDown;

        NowPointerButtons _capturedButtons;

        bool _leftDown;

        bool _rightDown;

        bool _upDown;

        bool _downDown;

        bool _submitDown;

        bool _cancelDown;

        bool _hostFocusKnown;

        bool _hostFocused;

        bool _pendingCaptureCancelled;

        NowTextInputFrame _textInputFrame;

        int _textInputPass = int.MinValue;

        int _keyboardClaimedPass = int.MinValue;

#if NOWUI_INPUT_SYSTEM
        NowKeyInputFrame _keyInputFrame;

        int _keyInputPass = int.MinValue;
#endif

        bool _textBackspaceDown;

        bool _textDeleteDown;

        bool _textLeftDown;

        bool _textRightDown;

        bool _textUpDown;

        bool _textDownDown;

        bool _textEnterDown;

        bool _textTabDown;

        public NowIMGUIInputProvider()
            : this(0, null)
        {
        }

        internal NowIMGUIInputProvider(int hostControlId)
            : this(hostControlId, null)
        {
        }

        internal NowIMGUIInputProvider(int hostControlId, object hostContext)
        {
            _hostControlId = hostControlId;
            _hostContext = hostContext;
        }

        internal object hostContext => _hostContext;

        internal bool isHostBacked => _hostControlId != 0;

        /// <summary>
        /// Returns the immutable text-editing packet captured from the native
        /// IMGUI event for this provider pass. The packet is copied before a
        /// focused control can mark the Event as Used.
        /// </summary>
        internal bool TryGetTextInputFrame(int inputPass, out NowTextInputFrame frame)
        {
            if (inputPass == _textInputPass)
            {
                frame = _textInputFrame;

                if (inputPass == _keyboardClaimedPass)
                    NowTextInput.SpendOneShotActivity(ref frame);

                return true;
            }

            frame = default;
            return false;
        }

#if NOWUI_INPUT_SYSTEM
        /// <summary>
        /// Returns the raw binding key captured from the native IMGUI event for
        /// this provider pass. Custom key sources remain authoritative.
        /// </summary>
        internal bool TryGetKeyInputFrame(int inputPass, out NowKeyInputFrame frame)
        {
            if (inputPass == _keyInputPass)
            {
                frame = inputPass == _keyboardClaimedPass
                    ? default
                    : _keyInputFrame;
                return true;
            }

            frame = default;
            return false;
        }

        internal void DiscardKeyInputFrame(int inputPass)
        {
            if (inputPass == _keyInputPass)
                _keyboardClaimedPass = inputPass;
        }
#endif

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            Event current = Event.current;

            if (current == null)
            {
                snapshot = default;
                return false;
            }

            EventType dispatchType = current.type;
            EventType nativeType = current.rawType;
            bool nativeKeyboardAvailable =
                dispatchType != EventType.Used &&
                IsKeyboardEvent(nativeType);
            EventType routedType = _hostControlId != 0 && !nativeKeyboardAvailable
                ? current.GetTypeForControl(_hostControlId)
                : nativeKeyboardAvailable ? nativeType : dispatchType;
            bool ownsCapture = _hostControlId != 0 && GUIUtility.hotControl == _hostControlId;
            return TryGetSnapshot(surface, current, routedType, ownsCapture, out snapshot);
        }

        /// <summary>
        /// Deterministic sampling seam for replaying native IMGUI event
        /// sequences in tests without depending on Event.current or a live
        /// EditorWindow.
        /// </summary>
        internal bool TryGetSnapshot(
            NowInputSurface surface,
            Event current,
            EventType routedType,
            bool ownsCapture,
            out NowInputSnapshot snapshot)
        {
            if (current == null ||
                !NowInput.TryScreenToSurface(current.mousePosition, surface, out var position))
            {
                snapshot = default;
                return false;
            }

            EventType dispatchType = current.type;
            EventType nativeType = current.rawType;

            // A panel uses a passive native control ID because NowFocus owns
            // keyboard focus internally. GetTypeForControl therefore filters
            // KeyDown/KeyUp to Ignore even when this panel owns the focused
            // NowUI field. Preserve native keyboard events and let the focused
            // NowUI host claim them; pointer events still use Unity's routed
            // type for hot-control capture.
            if (dispatchType != EventType.Used && IsKeyboardEvent(nativeType))
                routedType = nativeType;

            _sampledEvent = current;
            _sampledType = routedType;
            bool inside = position.x >= 0f && position.y >= 0f &&
                position.x <= surface.size.x && position.y <= surface.size.y;

            // Stale global interaction state can be cleared while another GUI
            // context is active, where touching GUIUtility.hotControl would
            // target that other context. The next event in this panel is the
            // safe point to release an orphaned native capture.
            if (ownsCapture &&
                _capturedButtons == NowPointerButtons.None &&
                _buttonsDown == NowPointerButtons.None)
            {
                ReleaseNativeCapture();
                ownsCapture = false;

                if (routedType == EventType.MouseDrag || routedType == EventType.MouseUp)
                    routedType = EventType.Ignore;
            }

            NowPointerButtons pressed = NowPointerButtons.None;
            NowPointerButtons released = NowPointerButtons.None;
            bool captureCancelled = _pendingCaptureCancelled;
            _pendingCaptureCancelled = false;
            bool lostNativeCapture =
                _hostControlId != 0 &&
                _capturedButtons != NowPointerButtons.None &&
                !ownsCapture;
            bool captureLossEvent =
                nativeType == EventType.Ignore ||
                nativeType == EventType.MouseLeaveWindow ||
                lostNativeCapture;

            if (captureLossEvent)
            {
                bool hadCapture =
                    _capturedButtons != NowPointerButtons.None ||
                    ownsCapture;
                captureCancelled |= hadCapture;
                _buttonsDown = NowPointerButtons.None;
                _capturedButtons = NowPointerButtons.None;

                if (ownsCapture)
                    ReleaseNativeCapture();

                if (hadCapture)
                    RequestHostRepaint();
            }
            else if (TryGetIMGUIButton(current.button, out var button))
            {
                var buttonMask = NowInputSnapshot.ToButtonMask(button);

                if (routedType == EventType.MouseDown && inside)
                {
                    pressed = buttonMask;
                    _buttonsDown |= buttonMask;
                }
                else if (routedType == EventType.MouseUp ||
                         (ownsCapture && current.rawType == EventType.MouseUp))
                {
                    released = buttonMask;
                    _buttonsDown &= ~buttonMask;
                    _capturedButtons &= ~buttonMask;

                    if (ownsCapture)
                    {
                        ConsumePointerEvent(current);

                        if (_capturedButtons == NowPointerButtons.None)
                            ReleaseNativeCapture();
                    }
                }
                else if (routedType == EventType.MouseDrag)
                {
                    if (ownsCapture)
                    {
                        _buttonsDown |= buttonMask;
                        ConsumePointerEvent(current);
                    }
                }
            }

            bool focusPreviousPressed = false;
            bool focusNextPressed = false;
            bool submitPressed = false;
            bool submitReleased = false;
            bool cancelPressed = false;
            bool cancelReleased = false;

            if (routedType == EventType.MouseDown)
                ResetTextKeyLatches();

            int inputPass = NextInputPass();
            _keyboardClaimedPass = int.MinValue;
            CaptureTextInput(current, routedType, inputPass);
#if NOWUI_INPUT_SYSTEM
            CaptureKeyInput(current, routedType, inputPass);
#endif

            if (routedType == EventType.KeyDown)
            {
                ApplyKeyDown(
                    current,
                    ref focusPreviousPressed,
                    ref focusNextPressed,
                    ref submitPressed,
                    ref cancelPressed);
            }
            else if (routedType == EventType.KeyUp)
            {
                ApplyKeyUp(current, ref submitReleased, ref cancelReleased);
            }

            Vector2 delta = NowInput.ScaleScreenDelta(current.delta, surface);
            Vector2 scrollDelta = routedType == EventType.ScrollWheel
                ? new Vector2(current.delta.x, -current.delta.y) / 3f
                : Vector2.zero;

            snapshot = new NowInputSnapshot(
                (!captureCancelled || routedType == EventType.MouseDown) &&
                    routedType != EventType.MouseLeaveWindow &&
                    (ownsCapture || inside),
                position,
                position - delta,
                delta,
                _buttonsDown,
                pressed,
                released,
                scrollDelta,
                ReadNavigation(),
                focusPreviousPressed,
                focusNextPressed,
                _submitDown,
                submitPressed,
                submitReleased,
                _cancelDown,
                cancelPressed,
                cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup)
            {
                inputPass = inputPass,
                pointerCaptureCancelled = captureCancelled
            };
            return true;
        }

        internal bool NotifyPointerCaptured(NowPointerButton button)
        {
            if (_sampledEvent == null || _sampledType != EventType.MouseDown)
                return false;

            if (_hostControlId != 0 &&
                GUIUtility.hotControl != 0 &&
                GUIUtility.hotControl != _hostControlId)
            {
                return false;
            }

            var buttonMask = NowInputSnapshot.ToButtonMask(button);
            _capturedButtons |= buttonMask;
            _buttonsDown |= buttonMask;

            if (_hostControlId != 0)
                GUIUtility.hotControl = _hostControlId;

            ConsumePointerEvent(_sampledEvent);
            return true;
        }

        internal void NotifyScrollConsumed()
        {
            Event current = _sampledEvent ?? Event.current;

            if (current == null || current.type != EventType.ScrollWheel)
                return;

            current.Use();
            RequestHostRepaint();
        }

        internal void NotifyFocusCleared()
        {
            if (_sampledType == EventType.MouseDown)
                ConsumePointerEvent(_sampledEvent ?? Event.current);
            else
                RequestHostRepaint();
        }

        internal void NotifyPointerPressConsumed()
        {
            if (_sampledType == EventType.MouseDown)
                ConsumePointerEvent(_sampledEvent ?? Event.current);
        }

        internal void NotifyTextActivityClaimed()
        {
            _keyboardClaimedPass = _textInputPass;
            ConsumeClaimedKeyEventForHost(_sampledEvent ?? Event.current);
        }

        internal void NotifyKeyActivityClaimed()
        {
#if NOWUI_INPUT_SYSTEM
            _keyboardClaimedPass = _keyInputPass;
#else
            _keyboardClaimedPass = _textInputPass;
#endif
            ConsumeClaimedKeyEventForHost(_sampledEvent ?? Event.current);
        }

        internal static void ConsumeScrollEvent(Event current)
        {
            if (current == null || current.type != EventType.ScrollWheel)
                return;

            current.Use();
            RequestRepaint();
        }

        internal static void ConsumeClaimedTextEvent(Event current)
        {
            ConsumeClaimedKeyEvent(current);
        }

        static void ConsumeClaimedKeyEvent(Event current)
        {
            if (current == null || current.type != EventType.KeyDown)
                return;

            current.Use();
            RequestRepaint();
        }

        void ConsumeClaimedKeyEventForHost(Event current)
        {
            if (current == null || current.type != EventType.KeyDown)
                return;

            current.Use();
            RequestHostRepaint();
        }

        void ConsumePointerEvent(Event current)
        {
            if (current != null && current.type != EventType.Used)
                current.Use();

            RequestHostRepaint();
        }

        internal static void RequestRepaint()
        {
            RequestRepaint(markGUIChanged: true);
        }

        internal static void RequestRepaint(bool markGUIChanged)
        {
            if (markGUIChanged)
                GUI.changed = true;

            repaintRequested?.Invoke();
        }

        internal void RequestHostRepaint(bool markGUIChanged = true)
        {
            if (markGUIChanged)
                GUI.changed = true;

            hostRepaintRequested?.Invoke(this);
            repaintRequested?.Invoke();
        }

        internal void RequestHostRepaintAfter(float delaySeconds)
        {
            if (delaySeconds <= 0f)
            {
                RequestHostRepaint(markGUIChanged: false);
                return;
            }

            if (hostRepaintAfterRequested != null)
                hostRepaintAfterRequested(this, delaySeconds);
        }

        internal void ApplyKeyDown(
            Event current,
            ref bool focusPreviousPressed,
            ref bool focusNextPressed,
            ref bool submitPressed,
            ref bool cancelPressed)
        {
            NowTextInput.Invalidate();
#if NOWUI_INPUT_SYSTEM
            NowKeyInput.Invalidate();
#endif
            var navigationKeys = NowInput.navigationKeys;

            switch (current.keyCode)
            {
                case KeyCode.LeftArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.A when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _leftDown = true;
                    break;
                case KeyCode.RightArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.D when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _rightDown = true;
                    break;
                case KeyCode.UpArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.W when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _upDown = true;
                    break;
                case KeyCode.DownArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.S when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _downDown = true;
                    break;
                case KeyCode.Tab when (navigationKeys & NowNavigationKeys.TabFocus) != 0:
                    if (current.shift)
                        focusPreviousPressed = true;
                    else
                        focusNextPressed = true;

                    break;
                case KeyCode.Return when (navigationKeys & NowNavigationKeys.EnterSubmit) != 0:
                case KeyCode.KeypadEnter when (navigationKeys & NowNavigationKeys.EnterSubmit) != 0:
                case KeyCode.Space when (navigationKeys & NowNavigationKeys.SpaceSubmit) != 0:
                    if (!_submitDown)
                        submitPressed = true;

                    _submitDown = true;
                    break;
                case KeyCode.Escape:
                    if (!_cancelDown)
                        cancelPressed = true;

                    _cancelDown = true;
                    break;
            }
        }

        internal void ApplyKeyUp(Event current, ref bool submitReleased, ref bool cancelReleased)
        {
            NowTextInput.Invalidate();
#if NOWUI_INPUT_SYSTEM
            NowKeyInput.Invalidate();
#endif

            switch (current.keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    _leftDown = false;
                    break;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    _rightDown = false;
                    break;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    _upDown = false;
                    break;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    _downDown = false;
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    if (_submitDown)
                    {
                        _submitDown = false;
                        submitReleased = true;
                    }

                    break;
                case KeyCode.Escape:
                    if (_cancelDown)
                    {
                        _cancelDown = false;
                        cancelReleased = true;
                    }

                    break;
            }
        }

        void CaptureTextInput(Event current, EventType eventType, int inputPass)
        {
            if (eventType == EventType.KeyDown)
                ApplyTextKeyDown(current.keyCode);
            else if (eventType == EventType.KeyUp)
                ApplyTextKeyUp(current.keyCode);

            var frame = new NowTextInputFrame
            {
                backspaceHeld = _textBackspaceDown,
                deleteHeld = _textDeleteDown,
                leftHeld = _textLeftDown,
                rightHeld = _textRightDown,
                upHeld = _textUpDown,
                downHeld = _textDownDown,
                enterHeld = _textEnterDown,
                tabHeld = _textTabDown,
                shift = current.shift,
                command = NowTextInput.isMacPlatform ? current.command : current.control,
                option = current.alt
            };

            if (eventType == EventType.KeyDown)
            {
                frame.homePressed = current.keyCode == KeyCode.Home;
                frame.endPressed = current.keyCode == KeyCode.End;
                frame.enterPressed = current.keyCode == KeyCode.Return ||
                    current.keyCode == KeyCode.KeypadEnter;
                frame.escapePressed = current.keyCode == KeyCode.Escape;
                frame.tabPressed = current.keyCode == KeyCode.Tab;
                frame.renamePressed = current.keyCode == KeyCode.F2;

                bool shortcutModifier = frame.command && !frame.option;

                if (shortcutModifier)
                {
                    frame.copyPressed = current.keyCode == KeyCode.C;
                    frame.pastePressed = current.keyCode == KeyCode.V;
                    frame.cutPressed = current.keyCode == KeyCode.X;
                    frame.selectAllPressed = current.keyCode == KeyCode.A;
                    frame.undoPressed = current.keyCode == KeyCode.Z && !frame.shift;
                    frame.redoPressed = current.keyCode == KeyCode.Y ||
                        (current.keyCode == KeyCode.Z && frame.shift);
                    frame.duplicatePressed = current.keyCode == KeyCode.D;
                    frame.commentPressed = current.keyCode == KeyCode.Slash;
                    frame.goToLinePressed = current.keyCode == KeyCode.G;
                }
                else if (current.character != '\0' && !char.IsControl(current.character))
                {
                    frame.characters = current.character.ToString();
                }
            }

            _textInputFrame = frame;
            _textInputPass = inputPass;
        }

#if NOWUI_INPUT_SYSTEM
        void CaptureKeyInput(Event current, EventType eventType, int inputPass)
        {
            _keyInputFrame = new NowKeyInputFrame
            {
                pressedKey = eventType == EventType.KeyDown
                    ? NowKeyInput.FromIMGUIKeyCode(current.keyCode)
                    : UnityEngine.InputSystem.Key.None
            };
            _keyInputPass = inputPass;
        }
#endif

        void ApplyTextKeyDown(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Backspace:
                    _textBackspaceDown = true;
                    break;
                case KeyCode.Delete:
                    _textDeleteDown = true;
                    break;
                case KeyCode.LeftArrow:
                    _textLeftDown = true;
                    break;
                case KeyCode.RightArrow:
                    _textRightDown = true;
                    break;
                case KeyCode.UpArrow:
                    _textUpDown = true;
                    break;
                case KeyCode.DownArrow:
                    _textDownDown = true;
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _textEnterDown = true;
                    break;
                case KeyCode.Tab:
                    _textTabDown = true;
                    break;
            }
        }

        void ApplyTextKeyUp(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Backspace:
                    _textBackspaceDown = false;
                    break;
                case KeyCode.Delete:
                    _textDeleteDown = false;
                    break;
                case KeyCode.LeftArrow:
                    _textLeftDown = false;
                    break;
                case KeyCode.RightArrow:
                    _textRightDown = false;
                    break;
                case KeyCode.UpArrow:
                    _textUpDown = false;
                    break;
                case KeyCode.DownArrow:
                    _textDownDown = false;
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _textEnterDown = false;
                    break;
                case KeyCode.Tab:
                    _textTabDown = false;
                    break;
            }
        }

        internal void ResetState()
        {
            ResetState(releaseNativeCapture: true);
        }

        /// <summary>
        /// Releases native capture, retained overlay ownership and buffered
        /// keyboard state. Custom IMGUI hosts should call this when they stop
        /// using the provider.
        /// </summary>
        public void Reset()
        {
            ResetState(releaseNativeCapture: true);
        }

        internal void ResetState(bool releaseNativeCapture)
        {
            NowOverlay.ReleaseRegistrationOwner(this);
            CancelTrackedCapture(releaseNativeCapture);
            _sampledEvent = null;
            _sampledType = EventType.Ignore;
            _pendingCaptureCancelled = false;
            ResetKeyboardLatches();
        }

        internal void CancelTrackedCapture(bool releaseNativeCapture)
        {
            _buttonsDown = NowPointerButtons.None;
            _capturedButtons = NowPointerButtons.None;

            if (releaseNativeCapture)
                ReleaseNativeCapture();
        }

        internal bool NotifyHostFocusChanged(bool focused, bool releaseNativeCapture)
        {
            if (!_hostFocusKnown)
            {
                _hostFocusKnown = true;
                _hostFocused = focused;
                return false;
            }

            if (_hostFocused == focused)
                return false;

            bool lostFocus = _hostFocused && !focused;
            _hostFocused = focused;

            if (!lostFocus)
                return false;

            if (_buttonsDown != NowPointerButtons.None ||
                _capturedButtons != NowPointerButtons.None)
            {
                _pendingCaptureCancelled = true;
            }

            CancelTrackedCapture(releaseNativeCapture);
            ResetKeyboardLatches();
            RequestHostRepaint(markGUIChanged: false);
            return true;
        }

        void ResetKeyboardLatches()
        {
            _leftDown = false;
            _rightDown = false;
            _upDown = false;
            _downDown = false;
            _submitDown = false;
            _cancelDown = false;
            _textInputFrame = default;
            _textInputPass = int.MinValue;
            _keyboardClaimedPass = int.MinValue;
#if NOWUI_INPUT_SYSTEM
            _keyInputFrame = default;
            _keyInputPass = int.MinValue;
#endif
            ResetTextKeyLatches();
        }

        void ResetTextKeyLatches()
        {
            _textBackspaceDown = false;
            _textDeleteDown = false;
            _textLeftDown = false;
            _textRightDown = false;
            _textUpDown = false;
            _textDownDown = false;
            _textEnterDown = false;
            _textTabDown = false;
        }

        static bool IsKeyboardEvent(EventType type)
        {
            return type == EventType.KeyDown || type == EventType.KeyUp;
        }

        Vector2 ReadNavigation()
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

            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        void ReleaseNativeCapture()
        {
            if (_hostControlId != 0 && GUIUtility.hotControl == _hostControlId)
                GUIUtility.hotControl = 0;
        }

        static int NextInputPass()
        {
            unchecked
            {
                ++s_inputPass;

                if (s_inputPass <= 0)
                    s_inputPass = 1;

                return s_inputPass;
            }
        }

        static bool TryGetIMGUIButton(int button, out NowPointerButton pointerButton)
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
