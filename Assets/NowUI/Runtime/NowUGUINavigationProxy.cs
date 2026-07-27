using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NowUI
{
    /// <summary>
    /// Represents a <see cref="NowGraphic"/> as one composite UGUI selectable.
    /// Directional input is routed through the focused NowUI control first and
    /// only falls back to the surrounding UGUI navigation graph at a true edge.
    /// </summary>
    [AddComponentMenu("NowUI/UGUI Navigation Proxy")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NowGraphic))]
    public sealed class NowUGUINavigationProxy : Selectable
    {
        [SerializeField, Tooltip("UGUI selectable that receives focus when Shift+Tab leaves this NowUI host. Falls back to Select On Up when unset.")]
        Selectable _tabPrevious;

        [SerializeField, Tooltip("UGUI selectable that receives focus when Tab leaves this NowUI host. Falls back to Select On Down when unset.")]
        Selectable _tabNext;

        NowGraphic _graphic;

        bool _selectionPending;

        /// <summary>
        /// UGUI selectable that receives focus when Shift+Tab reaches the first
        /// NowUI control. When unset, the proxy's Select On Up target is used.
        /// </summary>
        public Selectable tabPrevious
        {
            get => _tabPrevious;
            set => _tabPrevious = value;
        }

        /// <summary>
        /// UGUI selectable that receives focus when Tab reaches the last NowUI
        /// control. When unset, the proxy's Select On Down target is used.
        /// </summary>
        public Selectable tabNext
        {
            get => _tabNext;
            set => _tabNext = value;
        }

        internal GameObject owningSelection => gameObject;

        protected override void Awake()
        {
            base.Awake();
            CacheGraphic();

            // Selectable automatically adopts the Graphic on its GameObject.
            // The proxy is structural and must not tint the entire NowUI surface.
            if (targetGraphic == _graphic)
                targetGraphic = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheGraphic();
            _graphic.AttachUGUINavigationProxy(this);
        }

        protected override void OnDisable()
        {
            _selectionPending = false;

            if (_graphic != null)
                _graphic.DetachUGUINavigationProxy(this);

            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            transition = Transition.None;
            targetGraphic = null;
            CacheGraphic();
        }
#endif

        public override void OnSelect(BaseEventData eventData)
        {
            _selectionPending = false;
            base.OnSelect(eventData);
            CacheGraphic();
            _graphic.MarkDirty();

            // A pointer press is routed by NowGraphic itself, which can focus the
            // exact control under the pointer. Seeding here would briefly focus an
            // unrelated edge control before that retained draw occurs.
            if (eventData is PointerEventData)
                return;

            Vector2 direction = default;

            if (eventData is AxisEventData axisEvent)
                TryGetDirection(axisEvent.moveDir, out direction);

            _graphic.EnterUGUINavigation(direction);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (_graphic != null)
                _graphic.MarkDirty();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData == null)
                return;

            if (!TryGetDirection(eventData.moveDir, out Vector2 direction))
            {
                base.OnMove(eventData);
                return;
            }

            CacheGraphic();
            NowFocusMoveResult result = _graphic.RouteUGUINavigation(direction);

            if (result == NowFocusMoveResult.Moved ||
                result == NowFocusMoveResult.Seeded)
            {
                _graphic.MarkDirty();
            }

            if (result != NowFocusMoveResult.Boundary &&
                result != NowFocusMoveResult.Unavailable)
            {
                eventData.Use();
                return;
            }

            // Match Selectable/InputField behavior: let UGUI resolve the move
            // only at an internal edge, or when this host has no live controls.
            base.OnMove(eventData);

            GameObject selected = eventData.selectedObject;

            if (selected != null && selected != gameObject)
                _graphic.ExitUGUINavigation();
        }

        internal bool TryYieldTab(int step)
        {
            Selectable target = step < 0 ? _tabPrevious : _tabNext;

            if (target == null)
                target = step < 0 ? FindSelectableOnUp() : FindSelectableOnDown();

            if (target == null ||
                target == this ||
                !target.IsActive() ||
                !target.IsInteractable())
            {
                return false;
            }

            var eventSystem = EventSystem.current;

            if (eventSystem == null || eventSystem.alreadySelecting)
                return false;

            eventSystem.SetSelectedGameObject(target.gameObject);

            if (eventSystem.currentSelectedGameObject != target.gameObject)
                return false;

            if (target is NowUGUINavigationProxy targetProxy)
            {
                targetProxy.CacheGraphic();
                NowFocusMoveResult result = targetProxy._graphic.EnterUGUITab(step);

                if (result == NowFocusMoveResult.Moved ||
                    result == NowFocusMoveResult.Seeded)
                {
                    targetProxy._graphic.MarkDirty();
                }
            }

            return true;
        }

        internal void RequestSelection()
        {
            _selectionPending = true;
        }

        void LateUpdate()
        {
            if (!_selectionPending)
                return;

            CacheGraphic();

            if (_graphic == null ||
                !_graphic.hasFocusedControl ||
                !IsActive() ||
                !IsInteractable())
            {
                _selectionPending = false;
                return;
            }

            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null || eventSystem.alreadySelecting)
                return;

            _selectionPending = false;
            eventSystem.SetSelectedGameObject(gameObject);
        }

        void CacheGraphic()
        {
            if (_graphic == null)
                _graphic = GetComponent<NowGraphic>();
        }

        static bool TryGetDirection(MoveDirection moveDirection, out Vector2 direction)
        {
            switch (moveDirection)
            {
                case MoveDirection.Left:
                    direction = Vector2.left;
                    return true;
                case MoveDirection.Right:
                    direction = Vector2.right;
                    return true;
                case MoveDirection.Up:
                    direction = Vector2.up;
                    return true;
                case MoveDirection.Down:
                    direction = Vector2.down;
                    return true;
                default:
                    direction = default;
                    return false;
            }
        }
    }
}
