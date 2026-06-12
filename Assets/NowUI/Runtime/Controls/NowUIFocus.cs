using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NowUI
{
    /// <summary>
    /// Keyboard/gamepad focus for immediate-mode controls. Focusable controls
    /// register their rect every frame as they draw; navigation resolves spatially
    /// against the previous frame's registry (immediate mode has no widget tree).
    /// Pointer interaction focuses controls explicitly (<see cref="Focus"/>), the
    /// navigation vector moves focus directionally, cancel clears it, and
    /// <see cref="SubmitPressed"/> lets the focused control activate from
    /// keyboard/gamepad submit.
    /// </summary>
    public static class NowUIFocus
    {
        struct Focusable
        {
            public int id;
            public Rect rect;
        }

        static readonly List<Focusable> _current = new List<Focusable>(32);

        static readonly List<Focusable> _previous = new List<Focusable>(32);

        static int _focusedId;

        static int _registryFrame = -1;

        static Vector2 _lastNavigation;

        /// <summary>
        /// Keeps NowUI focus and Unity's EventSystem selection mutually exclusive
        /// (default on): selecting a UGUI control clears NowUI focus and pauses
        /// NowUI navigation, and focusing a NowUI control deselects the EventSystem.
        /// Cross-system navigation handoff is not attempted.
        /// </summary>
        public static bool respectEventSystem = true;

        /// <summary>The focused control id, or 0 when nothing has focus.</summary>
        public static int focusedId => _focusedId;

        public static bool IsFocused(int id)
        {
            return id != 0 && _focusedId == id;
        }

        public static void Focus(int id)
        {
            _focusedId = id;

            if (respectEventSystem && id != 0)
            {
                var eventSystem = EventSystem.current;

                if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                    eventSystem.SetSelectedGameObject(null);
            }
        }

        public static void Clear()
        {
            _focusedId = 0;
        }

        /// <summary>
        /// Adds a control to this frame's focus registry. Call every frame from the
        /// control's draw, after input interaction; ignored during layout measure
        /// passes so callback-form layouts don't register twice.
        /// </summary>
        public static void Register(int id, NowRect rect)
        {
            if (id == 0 || NowUIInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _current.Add(new Focusable { id = id, rect = (Rect)rect });
        }

        /// <summary>
        /// True when the focused control should activate from submit (enter/space/
        /// gamepad south) this frame.
        /// </summary>
        public static bool SubmitPressed(int id)
        {
            return IsFocused(id) && !NowUIInput.isPassive && NowUIInput.current.submitPressed;
        }

        static void BeginFrameIfNeeded()
        {
            int frame = Time.frameCount;

            if (_registryFrame == frame)
                return;

            _registryFrame = frame;

            _previous.Clear();
            _previous.AddRange(_current);
            _current.Clear();
            ProcessNavigation();
        }

        /// <summary>Forces the frame swap; used by tests where frameCount is static.</summary>
        internal static void ForceNewFrame()
        {
            _registryFrame = -1;
            BeginFrameIfNeeded();
            _registryFrame = -1;
        }

        static void ProcessNavigation()
        {
            var snapshot = NowUIInput.current;

            // While a UGUI control is selected, the EventSystem owns focus and
            // navigation; NowUI stands down until that selection clears.
            if (respectEventSystem)
            {
                var eventSystem = EventSystem.current;

                if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                {
                    Clear();
                    _lastNavigation = snapshot.navigation;
                    return;
                }
            }

            if (snapshot.cancelPressed)
                Clear();

            // Edge-detect the navigation vector so one stick flick moves one step.
            Vector2 navigation = snapshot.navigation;
            const float Threshold = 0.55f;

            Vector2 direction = default;

            // Navigation y+ means "up"; focus rect space is y-down screen coords.
            if (navigation.x > Threshold && _lastNavigation.x <= Threshold)
                direction = new Vector2(1f, 0f);
            else if (navigation.x < -Threshold && _lastNavigation.x >= -Threshold)
                direction = new Vector2(-1f, 0f);
            else if (navigation.y > Threshold && _lastNavigation.y <= Threshold)
                direction = new Vector2(0f, -1f);
            else if (navigation.y < -Threshold && _lastNavigation.y >= -Threshold)
                direction = new Vector2(0f, 1f);

            _lastNavigation = navigation;

            if (direction == default || _previous.Count == 0)
                return;

            MoveFocus(direction);
        }

        static void MoveFocus(Vector2 direction)
        {
            // Nothing focused (or the focused control vanished): take the first
            // registered control instead of guessing a direction from nowhere.
            int focusedIndex = -1;

            for (int i = 0; i < _previous.Count; ++i)
            {
                if (_previous[i].id == _focusedId)
                {
                    focusedIndex = i;
                    break;
                }
            }

            if (focusedIndex < 0)
            {
                _focusedId = _previous[0].id;
                return;
            }

            Vector2 origin = _previous[focusedIndex].rect.center;
            float bestScore = float.MaxValue;
            int bestId = 0;

            for (int i = 0; i < _previous.Count; ++i)
            {
                if (i == focusedIndex)
                    continue;

                Vector2 toCandidate = _previous[i].rect.center - origin;
                float along = Vector2.Dot(toCandidate, direction);

                if (along <= 0.5f)
                    continue; // behind or beside the direction of travel

                // Distance along the direction dominates; sideways drift is taxed so
                // navigation prefers aligned neighbors over closer diagonal ones.
                float sideways = (toCandidate - direction * along).magnitude;
                float score = along + sideways * 2.5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestId = _previous[i].id;
                }
            }

            if (bestId != 0)
                _focusedId = bestId;
        }

        public static void Reset()
        {
            _current.Clear();
            _previous.Clear();
            _focusedId = 0;
            _registryFrame = -1;
            _lastNavigation = default;
            respectEventSystem = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
