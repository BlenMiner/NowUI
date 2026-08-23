#if NOWUI_UGUI
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
        enum PendingYieldKind
        {
            None,
            Directional,
            Tab
        }

        [SerializeField, Tooltip("UGUI selectable that receives focus when Shift+Tab leaves this NowUI host. Falls back to Select On Up when unset.")]
        Selectable _tabPrevious;

        [SerializeField, Tooltip("UGUI selectable that receives focus when Tab leaves this NowUI host. Falls back to Select On Down when unset.")]
        Selectable _tabNext;

        NowGraphic _graphic;

        bool _selectionPending;

        PendingYieldKind _pendingYieldKind;

        Vector2 _pendingYieldDirection;

        int _pendingYieldTabStep;

        NowResolvedId _pendingYieldFocusId;

        int _pendingYieldFocusRevision;

        bool _pendingYieldWaitedForRegistryCommit;

        int _pendingInboundTabStep;

        FocusAdapter _focusAdapter;

        internal bool hasPendingSelection => _selectionPending;

        internal INowFocusNavigationProxy focusAdapter => _focusAdapter ??= new FocusAdapter(this);

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

        sealed class FocusAdapter : INowFocusNavigationProxy
        {
            readonly NowUGUINavigationProxy _owner;

            public FocusAdapter(NowUGUINavigationProxy owner)
            {
                _owner = owner;
            }

            public bool hasPendingSelection => _owner.hasPendingSelection;

            public GameObject owningSelection => _owner.owningSelection;

            public bool isActiveAndInteractable => _owner.IsActive() && _owner.IsInteractable();

            public void RequestSelection() => _owner.RequestSelection();

            public bool QueueYieldTab(int step) => _owner.QueueYieldTab(step);

            public bool QueueYieldDirection(Vector2 direction) => _owner.QueueYieldDirection(direction);

            public bool TryYieldTab(int step) => _owner.TryYieldTab(step);
        }

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
            _pendingInboundTabStep = 0;
            CancelPendingYield();

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
            _graphic.PrepareUGUIEntry();
            bool deferEntry = _graphic.ShouldDeferUGUIEntry();
            int inboundTabStep = _pendingInboundTabStep;
            _pendingInboundTabStep = 0;
            _graphic.MarkDirty();

            // A pointer press is routed by NowGraphic itself, which can focus the
            // exact control under the pointer. Seeding here would briefly focus an
            // unrelated edge control before that retained draw occurs.
            if (eventData is PointerEventData)
            {
                _graphic.CancelPendingUGUIEntry();
                _graphic.DiscardUGUIDirectionalReturn();
                return;
            }

            if (inboundTabStep != 0)
            {
                if (deferEntry)
                    _graphic.DeferUGUITabEntry(inboundTabStep);
                else
                    _graphic.EnterUGUITab(inboundTabStep);

                return;
            }

            Vector2 direction = default;

            if (eventData is AxisEventData axisEvent)
                TryGetDirection(axisEvent.moveDir, out direction);

            if (deferEntry)
                _graphic.DeferUGUINavigationEntry(direction);
            else
                _graphic.EnterUGUINavigation(direction);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (_graphic != null)
            {
                _pendingInboundTabStep = 0;
                _graphic.CancelPendingUGUIEntry();
                _graphic.CancelDeferredUGUINavigationBoundary();
                CancelPendingYield();
                _graphic.MarkDirty();
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            CacheGraphic();
            _pendingInboundTabStep = 0;
            _graphic.CancelPendingUGUIEntry();
            _graphic.CancelDeferredUGUINavigationBoundary();
            _graphic.DiscardUGUIDirectionalReturn();
            CancelPendingYield();
            base.OnPointerDown(eventData);
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

            if (_graphic.TryDeferUGUINavigation(direction))
            {
                eventData.Use();
                return;
            }

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

            TryYieldDirection(
                eventData,
                rememberDirectionalReturn:
                    result == NowFocusMoveResult.Boundary);
        }

        internal bool TryYieldTab(int step)
        {
            Selectable target = FindTabTarget(step);

            if (target == null ||
                target.gameObject == gameObject ||
                !target.IsActive() ||
                !target.IsInteractable())
            {
                return false;
            }

            var eventSystem = EventSystem.current;

            if (eventSystem == null ||
                eventSystem.alreadySelecting ||
                eventSystem.currentSelectedGameObject != gameObject)
            {
                return false;
            }

            // Clear before selection dispatch so a destination OnSelect callback
            // that explicitly focuses NowUI is authoritative.
            CacheGraphic();
            _graphic.ExitUGUINavigation();
            NowUGUINavigationProxy targetProxy =
                target as NowUGUINavigationProxy;

            if (targetProxy != null)
                targetProxy._pendingInboundTabStep = step < 0 ? -1 : 1;

            eventSystem.SetSelectedGameObject(target.gameObject);

            if (eventSystem.currentSelectedGameObject != target.gameObject)
            {
                if (targetProxy != null)
                    targetProxy._pendingInboundTabStep = 0;

                return false;
            }

            return true;
        }

        internal bool QueueYieldTab(int step)
        {
            Selectable target = FindTabTarget(step);

            if (!CanYieldTo(target, requireInteractable: true))
                return false;

            QueuePendingYield(
                PendingYieldKind.Tab,
                default,
                step);
            return true;
        }

        internal bool TryYieldDirection(
            Vector2 direction,
            bool rememberDirectionalReturn)
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null ||
                !TryGetMoveDirection(direction, out MoveDirection moveDirection))
            {
                return false;
            }

            var eventData = new AxisEventData(eventSystem)
            {
                moveDir = moveDirection,
                moveVector = direction
            };

            return TryYieldDirection(eventData, rememberDirectionalReturn);
        }

        internal bool QueueYieldDirection(Vector2 direction)
        {
            if (!TryGetMoveDirection(direction, out MoveDirection moveDirection) ||
                !CanYieldTo(
                    FindDirectionalTarget(moveDirection),
                    requireInteractable: false))
            {
                return false;
            }

            QueuePendingYield(
                PendingYieldKind.Directional,
                direction,
                0);
            return true;
        }

        bool TryYieldDirection(
            AxisEventData eventData,
            bool rememberDirectionalReturn)
        {
            Selectable target = FindDirectionalTarget(eventData.moveDir);

            if (target == null ||
                target.gameObject == gameObject ||
                !target.IsActive())
            {
                return false;
            }

            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null ||
                eventSystem.alreadySelecting ||
                eventSystem.currentSelectedGameObject != gameObject)
            {
                return false;
            }

            // Resolve the source before selection dispatch. Destination OnSelect
            // callbacks can then focus NowUI without being cleared afterward.
            CacheGraphic();

            if (rememberDirectionalReturn)
                _graphic.ExitUGUINavigationAtDirectionalBoundary();
            else
                _graphic.ExitUGUINavigation();

            eventSystem.SetSelectedGameObject(target.gameObject, eventData);
            return eventSystem.currentSelectedGameObject == target.gameObject;
        }

        void QueuePendingYield(
            PendingYieldKind kind,
            Vector2 direction,
            int tabStep)
        {
            _pendingYieldKind = kind;
            _pendingYieldDirection = direction;
            _pendingYieldTabStep = tabStep;
            _pendingYieldFocusId = NowFocus.focusedResolvedId;
            _pendingYieldFocusRevision = NowFocus.focusRevision;
            _pendingYieldWaitedForRegistryCommit = false;
        }

        internal void CancelPendingYield()
        {
            _pendingYieldKind = PendingYieldKind.None;
            _pendingYieldDirection = default;
            _pendingYieldTabStep = 0;
            _pendingYieldFocusId = NowResolvedId.None;
            _pendingYieldFocusRevision = 0;
            _pendingYieldWaitedForRegistryCommit = false;
        }

        internal bool ProcessPendingYield()
        {
            if (_pendingYieldKind == PendingYieldKind.None)
                return false;

            if (CanvasUpdateRegistry.IsRebuildingGraphics() ||
                CanvasUpdateRegistry.IsRebuildingLayout())
            {
                return true;
            }

            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null ||
                eventSystem.currentSelectedGameObject != gameObject ||
                NowFocus.focusedResolvedId != _pendingYieldFocusId ||
                NowFocus.focusRevision != _pendingYieldFocusRevision)
            {
                CancelPendingYield();
                return true;
            }

            if ((_graphic.hasDirtyFocusRegistry ||
                 _graphic.wantsFocusRegistryConvergence) &&
                !_pendingYieldWaitedForRegistryCommit)
            {
                _pendingYieldWaitedForRegistryCommit = true;
                _graphic.ScheduleFocusRegistryCommit();
                return true;
            }

            PendingYieldKind kind = _pendingYieldKind;
            Vector2 direction = _pendingYieldDirection;
            int tabStep = _pendingYieldTabStep;
            CancelPendingYield();

            NowFocusMoveResult result = kind == PendingYieldKind.Tab
                ? _graphic.RouteUGUITab(tabStep)
                : _graphic.RouteUGUINavigation(direction);

            if (result == NowFocusMoveResult.Moved ||
                result == NowFocusMoveResult.Seeded)
            {
                _graphic.MarkDirty();
                return true;
            }

            if (result == NowFocusMoveResult.Consumed)
                return true;

            if (kind == PendingYieldKind.Tab)
            {
                TryYieldTab(tabStep);
            }
            else
            {
                TryYieldDirection(
                    direction,
                    rememberDirectionalReturn:
                        result == NowFocusMoveResult.Boundary);
            }

            return true;
        }

        internal void RequestSelection()
        {
            _selectionPending = true;
        }

        void LateUpdate()
        {
            if (ProcessPendingYield())
                return;

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

        Selectable FindDirectionalTarget(MoveDirection moveDirection)
        {
            return moveDirection switch
            {
                MoveDirection.Left => FindSelectableOnLeft(),
                MoveDirection.Right => FindSelectableOnRight(),
                MoveDirection.Up => FindSelectableOnUp(),
                MoveDirection.Down => FindSelectableOnDown(),
                _ => null
            };
        }

        Selectable FindTabTarget(int step)
        {
            Selectable target = step < 0 ? _tabPrevious : _tabNext;

            if (target == null)
                target = step < 0 ? FindSelectableOnUp() : FindSelectableOnDown();

            return target;
        }

        bool CanYieldTo(Selectable target, bool requireInteractable)
        {
            EventSystem eventSystem = EventSystem.current;

            return target != null &&
                target.gameObject != gameObject &&
                target.IsActive() &&
                (!requireInteractable || target.IsInteractable()) &&
                eventSystem != null &&
                eventSystem.currentSelectedGameObject == gameObject;
        }

        static bool TryGetMoveDirection(
            Vector2 direction,
            out MoveDirection moveDirection)
        {
            float x = Mathf.Abs(direction.x);
            float y = Mathf.Abs(direction.y);

            if (x <= 0.5f && y <= 0.5f)
            {
                moveDirection = MoveDirection.None;
                return false;
            }

            if (x >= y)
            {
                moveDirection = direction.x < 0f
                    ? MoveDirection.Left
                    : MoveDirection.Right;
            }
            else
            {
                moveDirection = direction.y < 0f
                    ? MoveDirection.Down
                    : MoveDirection.Up;
            }

            return true;
        }
    }
}
#endif
