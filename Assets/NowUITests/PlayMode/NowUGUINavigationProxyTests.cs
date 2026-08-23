#if NOWUI_UGUI
using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NowUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class NowUGUINavigationProxyTests
{
    const int LeftKey = 101;
    const int RightKey = 102;
    const int LastKey = 103;

    static readonly Vector2 Surface = new Vector2(320f, 180f);

    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class TestNowGraphic : NowGraphic
    {
        public bool throwOnDraw;
        public bool drawTestControls;
        public bool horizontalTestControls;
        public bool directionalTestLayout;
        public MoveDirection directionalTestMove;
        public bool staggerDirectionalTestLayout;
        public bool includeFirstControl = true;
        public bool includeMiddleControl = true;
        public bool includeLastControl;
        public bool addLastOnNextDraw;
        public bool markDirtyAtEndOfDraw;
        public bool alwaysMarkDirtyAtEndOfDraw;
        public bool requestRepaintAtEndOfDraw;
        public int drawCount;
        public NowResolvedId focusedSeenDuringDraw;
        public NowResolvedId focusDuringDrawId;
        public INowInputProvider inputProvider;

        public void RebuildGeometryForTest()
        {
            UpdateGeometry();
        }

        public void RebuildPreRenderForTest()
        {
            Rebuild(CanvasUpdate.PreRender);
        }

        public void RunLateUpdateForTest()
        {
            base.LateUpdate();
        }

        protected override void DrawNowUI(NowRect rect)
        {
            if (throwOnDraw)
                throw new InvalidOperationException("Intentional proxy host failure.");

            if (!drawTestControls)
                return;

            ++drawCount;
            bool drawLastControl = includeLastControl;
            NowRect firstRect;
            NowRect middleRect;
            NowRect lastRect;

            if (directionalTestLayout)
            {
                bool horizontal = directionalTestMove == MoveDirection.Left ||
                    directionalTestMove == MoveDirection.Right;
                bool reverse = directionalTestMove == MoveDirection.Left ||
                    directionalTestMove == MoveDirection.Up;
                float firstAlong = reverse ? 110f : 10f;
                float lastAlong = reverse ? 10f : 110f;

                firstRect = horizontal
                    ? new NowRect(reverse ? 210f : 10f, 10f, 60f, 30f)
                    : new NowRect(
                        staggerDirectionalTestLayout ? 210f : 10f,
                        firstAlong,
                        60f,
                        30f);
                middleRect = horizontal
                    ? new NowRect(110f, 10f, 60f, 30f)
                    : new NowRect(
                        staggerDirectionalTestLayout ? 110f : 10f,
                        60f,
                        60f,
                        30f);
                lastRect = horizontal
                    ? new NowRect(reverse ? 10f : 210f, 10f, 60f, 30f)
                    : new NowRect(10f, lastAlong, 60f, 30f);
            }
            else
            {
                firstRect = new NowRect(10f, 10f, 60f, 30f);
                middleRect = horizontalTestControls
                    ? new NowRect(110f, 10f, 60f, 30f)
                    : new NowRect(10f, 60f, 60f, 30f);
                lastRect = horizontalTestControls
                    ? new NowRect(210f, 10f, 60f, 30f)
                    : new NowRect(10f, 110f, 60f, 30f);
            }

            if (includeFirstControl)
                NowFocus.Register(ResolveControlId(LeftKey), firstRect);

            if (includeMiddleControl)
                NowFocus.Register(ResolveControlId(RightKey), middleRect);

            if (drawLastControl)
                NowFocus.Register(ResolveControlId(LastKey), lastRect);

            focusedSeenDuringDraw = NowFocus.focusedResolvedId;

            if (focusDuringDrawId.hasValue)
            {
                NowResolvedId id = focusDuringDrawId;
                focusDuringDrawId = default;
                NowFocus.Focus(id);
            }

            if (addLastOnNextDraw)
            {
                addLastOnNextDraw = false;
                includeLastControl = true;
            }

            if (markDirtyAtEndOfDraw)
            {
                markDirtyAtEndOfDraw = false;
                MarkDirty();
            }

            if (alwaysMarkDirtyAtEndOfDraw)
                MarkDirty();

            if (requestRepaintAtEndOfDraw)
            {
                requestRepaintAtEndOfDraw = false;
                NowControlState.RequestRepaint();
            }
        }

        protected override INowInputProvider GetInputProvider()
        {
            return inputProvider ?? base.GetInputProvider();
        }
    }

    sealed class FocusOnSelect : MonoBehaviour, ISelectHandler
    {
        public NowResolvedId id;

        public void OnSelect(BaseEventData eventData)
        {
            NowFocus.Focus(id);
        }
    }

    sealed class YieldOnSelect : MonoBehaviour, ISelectHandler
    {
        public NowUGUINavigationProxy proxy;
        public bool yielded;

        public void OnSelect(BaseEventData eventData)
        {
            yielded = proxy.TryYieldDirection(
                Vector2.right,
                rememberDirectionalReturn: true);
        }
    }

    readonly FakeProvider _provider = new FakeProvider();

    GameObject _eventSystemObject;
    GameObject _hostObject;
    GameObject _outsideObject;
    EventSystem _eventSystem;
    TestNowGraphic _graphic;

    NowResolvedId LeftId => _graphic.ResolveControlId(LeftKey);

    NowResolvedId RightId => _graphic.ResolveControlId(RightKey);

    NowResolvedId LastId => _graphic.ResolveControlId(LastKey);
    NowUGUINavigationProxy _proxy;
    Button _outside;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowOverlay.Reset();
        NowFocus.respectEventSystem = true;

        _eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        _eventSystem = _eventSystemObject.GetComponent<EventSystem>();
        Assert.AreSame(_eventSystem, EventSystem.current);

        _hostObject = new GameObject("Now Host", typeof(RectTransform));
        _graphic = _hostObject.AddComponent<TestNowGraphic>();
        _proxy = _hostObject.AddComponent<NowUGUINavigationProxy>();

        _outsideObject = new GameObject("Outside UGUI", typeof(RectTransform));
        _outside = _outsideObject.AddComponent<Button>();

        var navigation = Navigation.defaultNavigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnRight = _outside;
        _proxy.navigation = navigation;

        // Establish the same clean retained-host state production reaches after
        // its first canvas pass. Individual tests dirty it explicitly when they
        // exercise pending-registry behavior.
        _graphic.RebuildGeometryForTest();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_outsideObject);
        Object.DestroyImmediate(_hostObject);
        Object.DestroyImmediate(_eventSystemObject);

        NowInput.Reset();
        NowFocus.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowOverlay.Reset();
    }

    [TestCase(MoveDirection.Left)]
    [TestCase(MoveDirection.Right)]
    [TestCase(MoveDirection.Up)]
    [TestCase(MoveDirection.Down)]
    public void DirectionalOwnerConsumesEveryMoveWithoutYieldingToUGUI(
        MoveDirection direction)
    {
        RegisterControls(NowFocusNavigationLock.Directional, includeRight: false);
        SelectProxy();

        AxisEventData move = Move(direction);
        _proxy.OnMove(move);

        Assert.IsTrue(move.used);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void MoveStaysInsideProxyWhileNowUIHasATarget()
    {
        RegisterControls();
        SelectProxy();
        CommitGraphicControls(horizontal: true);

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsTrue(move.used);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
    }

    [TestCase(MoveDirection.Left)]
    [TestCase(MoveDirection.Right)]
    [TestCase(MoveDirection.Up)]
    [TestCase(MoveDirection.Down)]
    public void LastInternalControlIsSelectedBeforeDirectionalMoveYieldsToUGUI(
        MoveDirection direction)
    {
        ConfigureOutsideTarget(direction);
        CommitDirectionalGraphicControls(direction);
        SelectProxy();
        CommitDirectionalGraphicControls(direction);

        AxisEventData moveToMiddle = Move(direction);
        _proxy.OnMove(moveToMiddle);

        Assert.IsTrue(moveToMiddle.used);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        CommitDirectionalGraphicControls(direction);

        AxisEventData moveToLast = Move(direction);
        _proxy.OnMove(moveToLast);

        Assert.IsTrue(moveToLast.used);
        Assert.AreEqual(LastId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        CommitDirectionalGraphicControls(direction);

        AxisEventData boundary = Move(direction);
        _proxy.OnMove(boundary);

        Assert.IsFalse(boundary.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void DirectionalReturnFromUGUIRestoresTheControlThatYielded()
    {
        ConfigureOutsideTarget(MoveDirection.Down);

        var outsideNavigation = Navigation.defaultNavigation;
        outsideNavigation.mode = Navigation.Mode.Explicit;
        outsideNavigation.selectOnLeft = _proxy;
        _outside.navigation = outsideNavigation;

        CommitDirectionalGraphicControls(
            MoveDirection.Down,
            staggerHorizontally: true);
        SelectProxy();
        NowFocus.Focus(LastId);
        CommitDirectionalGraphicControls(
            MoveDirection.Down,
            staggerHorizontally: true);

        AxisEventData boundary = Move(MoveDirection.Down);
        _proxy.OnMove(boundary);

        Assert.IsFalse(boundary.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);

        _outside.OnMove(Move(MoveDirection.Left));
        CommitDirectionalGraphicControls(
            MoveDirection.Down,
            staggerHorizontally: true);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            LastId,
            NowFocus.focusedResolvedId,
            "Returning through the composite proxy must restore the internal control " +
            "that yielded, even when a different control is farther along the inbound axis.");
    }

    [Test]
    public void RemovedDirectionalReturnTargetFallsBackToTheMatchingEdge()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        Assert.IsTrue(
            _proxy.TryYieldDirection(
                Vector2.right,
                rememberDirectionalReturn: true));

        _provider.snapshot = NavigationSnapshot(frame: 2);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy.focusAdapter))
        {
            NowFocus.Register(
                RightId,
                new NowRect(110f, 10f, 60f, 30f));
        }

        var outsideNavigation = Navigation.defaultNavigation;
        outsideNavigation.mode = Navigation.Mode.Explicit;
        outsideNavigation.selectOnLeft = _proxy;
        _outside.navigation = outsideNavigation;
        _outside.OnMove(Move(MoveDirection.Left));
        CommitGraphicControls(
            horizontal: true,
            includeFirst: false);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            RightId,
            NowFocus.focusedResolvedId,
            "A removed return target must fall back to spatial edge entry.");
    }

    [Test]
    public void DirtyRequestedDuringDrawDefersBoundaryUntilNewLastCommits()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;

        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        SelectProxy();
        NowFocus.Focus(RightId);

        _graphic.addLastOnNextDraw = true;
        _graphic.markDirtyAtEndOfDraw = true;
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        Assert.AreEqual(2, _graphic.drawCount);

        // Graphic.Rebuild clears UGUI's private vertex-dirty bit after this
        // draw. MarkDirty inside DrawNowUI must still be remembered by NowUI.
        _graphic.RebuildPreRenderForTest();
        Assert.AreEqual(2, _graphic.drawCount);

        AxisEventData move = Move(MoveDirection.Down);
        _proxy.OnMove(move);

        Assert.IsTrue(move.used);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        Assert.AreEqual(
            2,
            _graphic.drawCount,
            "Navigation must not force an interactive redraw inside OnMove.");

        _graphic.RebuildPreRenderForTest();
        _proxy.ProcessPendingYield();

        Assert.AreEqual(
            LastId,
            NowFocus.focusedResolvedId,
            "The deferred move must resolve against the newly committed registry.");
        Assert.AreEqual(
            RightId,
            _graphic.focusedSeenDuringDraw,
            "The move must resolve after the draw commits, not against a partial list.");
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(3, _graphic.drawCount);

        // The post-commit focus change requests a retained visual repaint.
        _graphic.RebuildPreRenderForTest();
        Assert.AreEqual(3, _graphic.drawCount);

        _graphic.RunLateUpdateForTest();
        _graphic.RebuildPreRenderForTest();

        Assert.AreEqual(4, _graphic.drawCount);
        Assert.AreEqual(LastId, _graphic.focusedSeenDuringDraw);

        _graphic.RunLateUpdateForTest();
        _graphic.RebuildPreRenderForTest();
        Assert.AreEqual(4, _graphic.drawCount);
    }

    [Test]
    public void RepaintConvergenceDefersBoundaryUntilNewLastCommits()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;

        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        SelectProxy();
        NowFocus.Focus(RightId);

        _graphic.addLastOnNextDraw = true;
        _graphic.requestRepaintAtEndOfDraw = true;
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        AxisEventData move = Move(MoveDirection.Down);
        _proxy.OnMove(move);

        Assert.IsTrue(move.used);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        Assert.AreEqual(2, _graphic.drawCount);

        _graphic.RebuildPreRenderForTest();

        Assert.AreEqual(LastId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(3, _graphic.drawCount);
    }

    [Test]
    public void QueuedYieldReroutesAfterAConvergenceDrawAddsTheLastControl()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;
        CommitGraphicControls();
        SelectProxy();
        NowFocus.Focus(RightId);
        CommitGraphicControls();

        _graphic.addLastOnNextDraw = true;
        _graphic.requestRepaintAtEndOfDraw = true;
        _graphic.MarkDirty();

        AxisEventData move = Move(MoveDirection.Down);
        _proxy.OnMove(move);
        Assert.IsTrue(move.used);

        _graphic.RebuildPreRenderForTest();

        Assert.IsTrue(_proxy.ProcessPendingYield());
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);

        _graphic.RebuildPreRenderForTest();
        Assert.IsTrue(_proxy.ProcessPendingYield());

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            LastId,
            NowFocus.focusedResolvedId,
            "A queued yield must be re-routed after repaint convergence.");
    }

    [Test]
    public void ContinuouslyDirtyHostEventuallyYieldsAtARealBoundary()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;
        CommitGraphicControls();
        SelectProxy();
        NowFocus.Focus(RightId);
        CommitGraphicControls();

        _graphic.alwaysMarkDirtyAtEndOfDraw = true;
        _graphic.MarkDirty();
        _proxy.OnMove(Move(MoveDirection.Down));
        _graphic.RebuildPreRenderForTest();

        Assert.IsTrue(_proxy.ProcessPendingYield());
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);

        _graphic.RebuildPreRenderForTest();
        Assert.IsTrue(_proxy.ProcessPendingYield());

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [UnityTest]
    public IEnumerator RealCanvasPassCommitsTheNewLastControlAutomatically()
    {
        var canvasObject = new GameObject(
            "Navigation Canvas",
            typeof(RectTransform),
            typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hostObject.transform.SetParent(canvasObject.transform, false);

        try
        {
            ConfigureOutsideTarget(MoveDirection.Down);
            _graphic.drawTestControls = true;

            RectTransform rect = _hostObject.GetComponent<RectTransform>();
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Surface.x);
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Surface.y);
            _graphic.MarkDirty();
            Canvas.ForceUpdateCanvases();

            SelectProxy();
            NowFocus.Focus(RightId);
            _graphic.addLastOnNextDraw = true;
            _graphic.markDirtyAtEndOfDraw = true;
            _graphic.MarkDirty();
            Canvas.ForceUpdateCanvases();

            int drawCountBeforeMove = _graphic.drawCount;
            AxisEventData move = Move(MoveDirection.Down);
            _proxy.OnMove(move);

            Assert.IsTrue(move.used);
            Assert.AreEqual(RightId, NowFocus.focusedResolvedId);

            yield return null;

            Assert.Greater(_graphic.drawCount, drawCountBeforeMove);
            Assert.AreSame(
                _hostObject,
                _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(
                LastId,
                NowFocus.focusedResolvedId,
                "The normal CanvasUpdateRegistry pass must resolve the move.");
        }
        finally
        {
            if (_hostObject != null)
                _hostObject.transform.SetParent(null);

            Object.DestroyImmediate(canvasObject);
        }
    }

    [UnityTest]
    public IEnumerator DeferredCanvasBoundaryHandsOffToAnotherProxySafely()
    {
        var canvasObject = new GameObject(
            "Proxy Handoff Canvas",
            typeof(RectTransform),
            typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hostObject.transform.SetParent(canvasObject.transform, false);

        var targetObject = new GameObject(
            "Target Now Host",
            typeof(RectTransform));
        targetObject.transform.SetParent(canvasObject.transform, false);
        var targetGraphic = targetObject.AddComponent<TestNowGraphic>();
        var targetProxy =
            targetObject.AddComponent<NowUGUINavigationProxy>();

        try
        {
            RectTransform sourceRect =
                _hostObject.GetComponent<RectTransform>();
            sourceRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Surface.x);
            sourceRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Surface.y);
            RectTransform targetRect =
                targetObject.GetComponent<RectTransform>();
            targetRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Surface.x);
            targetRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Surface.y);

            _graphic.drawTestControls = true;
            targetGraphic.drawTestControls = true;

            var navigation = Navigation.defaultNavigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnDown = targetProxy;
            _proxy.navigation = navigation;

            _graphic.MarkDirty();
            targetGraphic.MarkDirty();
            Canvas.ForceUpdateCanvases();

            SelectProxy();
            NowFocus.Focus(RightId);
            Canvas.ForceUpdateCanvases();

            int targetDrawCount = targetGraphic.drawCount;
            _graphic.MarkDirty();
            AxisEventData move = Move(MoveDirection.Down);
            _proxy.OnMove(move);
            Assert.IsTrue(move.used);

            yield return null;

            Assert.AreSame(
                _hostObject,
                _eventSystem.currentSelectedGameObject,
                "Canvas commit should queue, not dispatch, the handoff.");

            yield return null;

            Assert.AreSame(
                targetObject,
                _eventSystem.currentSelectedGameObject);
            Assert.IsTrue(targetGraphic.hasFocusedControl);
            Assert.Greater(targetGraphic.drawCount, targetDrawCount);
        }
        finally
        {
            if (_hostObject != null)
                _hostObject.transform.SetParent(null);

            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void RepeatedBoundaryMoveWaitsUntilTheLastControlDrawsFocused()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;

        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        SelectProxy();
        NowFocus.Focus(RightId);

        _graphic.addLastOnNextDraw = true;
        _graphic.markDirtyAtEndOfDraw = true;
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        _proxy.OnMove(Move(MoveDirection.Down));
        _graphic.RebuildPreRenderForTest();

        Assert.AreEqual(LastId, NowFocus.focusedResolvedId);
        Assert.AreEqual(RightId, _graphic.focusedSeenDuringDraw);

        AxisEventData repeatedBoundary = Move(MoveDirection.Down);
        _proxy.OnMove(repeatedBoundary);

        Assert.IsTrue(repeatedBoundary.used);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LastId, NowFocus.focusedResolvedId);

        _graphic.RebuildPreRenderForTest();

        Assert.AreEqual(
            LastId,
            _graphic.focusedSeenDuringDraw,
            "The last control must render focused before the repeated move can yield.");
        _proxy.ProcessPendingYield();
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void DirtyBoundaryYieldsOnlyAfterThePendingRegistryCommits()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;

        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        SelectProxy();
        NowFocus.Focus(RightId);

        _graphic.markDirtyAtEndOfDraw = true;
        _graphic.MarkDirty();
        _graphic.RebuildPreRenderForTest();

        AxisEventData move = Move(MoveDirection.Down);
        _proxy.OnMove(move);

        Assert.IsTrue(move.used);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);

        _graphic.RebuildPreRenderForTest();
        _proxy.ProcessPendingYield();

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void DeferredMoveIsCanceledWhenProxyIsDeselectedAndReselected()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;
        CommitGraphicControls();
        SelectProxy();
        NowFocus.Focus(RightId);

        _graphic.includeLastControl = true;
        _graphic.MarkDirty();

        AxisEventData move = Move(MoveDirection.Down);
        _proxy.OnMove(move);
        Assert.IsTrue(move.used);

        _eventSystem.SetSelectedGameObject(_outsideObject);
        _eventSystem.SetSelectedGameObject(_hostObject);
        _graphic.RebuildPreRenderForTest();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            RightId,
            NowFocus.focusedResolvedId,
            "A move from an earlier proxy-selection lifetime must not replay.");
    }

    [Test]
    public void CullingCancelsADeferredMove()
    {
        ConfigureOutsideTarget(MoveDirection.Down);
        _graphic.drawTestControls = true;
        CommitGraphicControls();
        SelectProxy();
        NowFocus.Focus(RightId);

        _graphic.includeLastControl = true;
        _graphic.MarkDirty();
        _proxy.OnMove(Move(MoveDirection.Down));

        _graphic.Cull(default, validRect: false);
        Assert.IsTrue(_graphic.canvasRenderer.cull);

        _graphic.RebuildPreRenderForTest();
        _graphic.canvasRenderer.cull = false;
        _graphic.RebuildPreRenderForTest();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            RightId,
            NowFocus.focusedResolvedId,
            "A move deferred before culling must not replay after un-culling.");
    }

    [Test]
    public void ExplicitFocusDuringDrawCancelsTheDeferredTabMove()
    {
        _proxy.tabNext = _outside;
        _graphic.drawTestControls = true;
        _graphic.horizontalTestControls = true;
        _graphic.inputProvider = _provider;
        CommitGraphicControls(horizontal: true);
        SelectProxy();
        NowFocus.Focus(RightId);

        _provider.snapshot = NavigationSnapshot(next: true, frame: 2);
        _graphic.focusDuringDrawId = LeftId;
        _graphic.MarkDirty();
        _graphic.RebuildGeometryForTest();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            LeftId,
            NowFocus.focusedResolvedId,
            "An explicit draw-time focus request must supersede the old Tab input.");
        Assert.IsFalse(_proxy.ProcessPendingYield());
    }

    [Test]
    public void NewControlFocusedDuringDrawIsNotReplacedByProxyEntry()
    {
        _graphic.drawTestControls = true;
        _graphic.horizontalTestControls = true;
        _graphic.includeMiddleControl = false;
        CommitGraphicControls(horizontal: true, includeMiddle: false);
        _eventSystem.SetSelectedGameObject(_outsideObject);

        _graphic.includeMiddleControl = true;
        _graphic.focusDuringDrawId = RightId;
        _graphic.MarkDirty();
        _graphic.RebuildGeometryForTest();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            RightId,
            NowFocus.focusedResolvedId,
            "Proxy OnSelect must preserve an explicit focus already registered " +
            "by the in-flight draw.");
    }

    [Test]
    public void DirtyHostCommitsANewLastControlBeforeTabBoundaryRouting()
    {
        _proxy.tabNext = _outside;
        _graphic.drawTestControls = true;
        _graphic.inputProvider = _provider;

        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);

        _provider.snapshot = NavigationSnapshot(frame: 1);
        _graphic.RebuildGeometryForTest();
        SelectProxy();
        NowFocus.Focus(RightId);

        _graphic.includeLastControl = true;
        _provider.snapshot = NavigationSnapshot(next: true, frame: 2);
        _graphic.MarkDirty();
        _graphic.RebuildGeometryForTest();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            LastId,
            NowFocus.focusedResolvedId,
            "Tab must resolve its boundary against the registry committed by the " +
            "current retained draw, not the previous frame's final control.");
    }

    [Test]
    public void ProxyMoveAndSameInputSnapshotRebuildAdvanceOnlyOnce()
    {
        RegisterControls();
        SelectProxy();
        _provider.snapshot = NavigationSnapshot(
            navigation: Vector2.right,
            frame: 2);

        _proxy.OnMove(Move(MoveDirection.Right));

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy.focusAdapter))
        {
            NowFocus.Register(LeftId, new NowRect(10f, 10f, 60f, 30f));
            NowFocus.Register(RightId, new NowRect(110f, 10f, 60f, 30f));
        }

        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void BoundaryMoveYieldsToConfiguredUGUISelectable()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(RightId);
        CommitGraphicControls();

        _proxy.OnMove(Move(MoveDirection.Right));

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void DirectionalSelfTargetDoesNotClearNowUIFocus()
    {
        var navigation = Navigation.defaultNavigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnRight = _proxy;
        _proxy.navigation = navigation;

        RegisterControls();
        SelectProxy();
        NowFocus.Focus(RightId);
        CommitGraphicControls();

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void ReentrantSelectionCannotClearFocusBeforeUGUICanHandoff()
    {
        RegisterControls();
        NowFocus.respectEventSystem = false;
        NowFocus.Focus(RightId);
        NowFocus.respectEventSystem = true;

        var yieldOnSelect = _hostObject.AddComponent<YieldOnSelect>();
        yieldOnSelect.proxy = _proxy;
        _eventSystem.SetSelectedGameObject(_hostObject);

        Assert.IsFalse(yieldOnSelect.yielded);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void UnavailableHostDoesNotRestoreAStaleFocusedControl()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);
        RegisterEmptyHost();

        NowFocusMoveResult result =
            _graphic.RouteUGUINavigation(Vector2.right);
        Assert.AreEqual(NowFocusMoveResult.Unavailable, result);
        Assert.IsTrue(
            _proxy.TryYieldDirection(
                Vector2.right,
                rememberDirectionalReturn:
                    result == NowFocusMoveResult.Boundary));
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);

        RegisterControls(frame: 2);

        var outsideNavigation = Navigation.defaultNavigation;
        outsideNavigation.mode = Navigation.Mode.Explicit;
        outsideNavigation.selectOnLeft = _proxy;
        _outside.navigation = outsideNavigation;
        _outside.OnMove(Move(MoveDirection.Left));
        CommitGraphicControls(horizontal: true);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            RightId,
            NowFocus.focusedResolvedId,
            "Unavailable routing must use spatial entry, not stale focus memory.");
    }

    [Test]
    public void PointerEntryDiscardsDirectionalReturnMemory()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        Assert.IsTrue(
            _proxy.TryYieldDirection(
                Vector2.right,
                rememberDirectionalReturn: true));
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);

        var pointerEntry = new PointerEventData(_eventSystem);
        _eventSystem.SetSelectedGameObject(_hostObject, pointerEntry);
        _eventSystem.SetSelectedGameObject(_outsideObject);
        RegisterControls(frame: 2);

        var outsideNavigation = Navigation.defaultNavigation;
        outsideNavigation.mode = Navigation.Mode.Explicit;
        outsideNavigation.selectOnLeft = _proxy;
        _outside.navigation = outsideNavigation;
        _outside.OnMove(Move(MoveDirection.Left));
        CommitGraphicControls(horizontal: true);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            RightId,
            NowFocus.focusedResolvedId,
            "Pointer entry must cancel the prior directional return target.");
    }

    [Test]
    public void PointerDownCancelsAQueuedUGUIHandoff()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(RightId);

        Assert.IsTrue(
            _proxy.QueueYieldDirection(Vector2.right));

        _proxy.OnPointerDown(new PointerEventData(_eventSystem));

        Assert.IsFalse(_proxy.ProcessPendingYield());
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void PointerEntryCancelsAStalePendingDirectionalEntry()
    {
        _eventSystem.SetSelectedGameObject(_outsideObject);

        AxisEventData axisEntry = Move(MoveDirection.Left);
        _eventSystem.SetSelectedGameObject(_hostObject, axisEntry);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);

        _eventSystem.SetSelectedGameObject(_outsideObject);
        _eventSystem.SetSelectedGameObject(
            _hostObject,
            new PointerEventData(_eventSystem));
        RegisterControls();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            NowResolvedId.None,
            NowFocus.focusedResolvedId,
            "Pointer entry must not replay the axis from an abandoned selection.");
    }

    [Test]
    public void InboundMoveSeedsTheMatchingNowUIEdge()
    {
        RegisterControls();
        _eventSystem.SetSelectedGameObject(_outsideObject);
        CommitGraphicControls(horizontal: true);

        AxisEventData enterFromRight = Move(MoveDirection.Left);
        _eventSystem.SetSelectedGameObject(_hostObject, enterFromRight);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void ProgrammaticNowUIFocusSelectsItsProxy()
    {
        RegisterControls();
        _eventSystem.SetSelectedGameObject(_outsideObject);

        NowFocus.Focus(LeftId);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);

        NowFocus.Focus(RightId);
        CommitGraphicControls();
        _proxy.OnMove(Move(MoveDirection.Right));

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void ProgrammaticSelectionBetweenProxiesSeedsTheDestinationHost()
    {
        RegisterControls();

        GameObject sourceObject = new GameObject(
            "Source Now Host",
            typeof(RectTransform));

        try
        {
            var sourceGraphic = sourceObject.AddComponent<TestNowGraphic>();
            var sourceProxy = sourceObject.AddComponent<NowUGUINavigationProxy>();
            sourceGraphic.RebuildGeometryForTest();

            _provider.snapshot = NavigationSnapshot(frame: 1);

            using (NowInput.Begin(_provider, Surface))
            using (NowFocus.BeginHostRegistration(
                sourceGraphic.focusHostId,
                sourceProxy.focusAdapter))
            {
                NowFocus.Register(
                    sourceGraphic.ResolveControlId(201),
                    new NowRect(10f, 10f, 60f, 30f));
            }

            _eventSystem.SetSelectedGameObject(sourceObject);
            Assert.AreEqual(
                sourceGraphic.ResolveControlId(201),
                NowFocus.focusedResolvedId);
            NowFocus.Focus(sourceGraphic.ResolveControlId(201));

            _eventSystem.SetSelectedGameObject(_hostObject);

            Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(
                LeftId,
                NowFocus.focusedResolvedId,
                "Selecting a different proxy must replace the previous host's " +
                "internal focus with the destination edge control.");
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
        }
    }

    [Test]
    public void ProgrammaticSelectionBetweenProxiesSeedsDirtyDestinationAfterCommit()
    {
        RegisterControls();

        GameObject sourceObject = new GameObject(
            "Source Now Host",
            typeof(RectTransform));

        try
        {
            var sourceGraphic = sourceObject.AddComponent<TestNowGraphic>();
            var sourceProxy = sourceObject.AddComponent<NowUGUINavigationProxy>();
            sourceGraphic.RebuildGeometryForTest();

            _provider.snapshot = NavigationSnapshot(frame: 1);

            using (NowInput.Begin(_provider, Surface))
            using (NowFocus.BeginHostRegistration(
                sourceGraphic.focusHostId,
                sourceProxy.focusAdapter))
            {
                NowFocus.Register(
                    sourceGraphic.ResolveControlId(201),
                    new NowRect(10f, 10f, 60f, 30f));
            }

            _eventSystem.SetSelectedGameObject(sourceObject);
            Assert.AreEqual(
                sourceGraphic.ResolveControlId(201),
                NowFocus.focusedResolvedId);

            _graphic.MarkDirty();
            _eventSystem.SetSelectedGameObject(_hostObject);

            Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(
                NowResolvedId.None,
                NowFocus.focusedResolvedId,
                "A dirty destination must wait for its retained registry commit.");

            CommitGraphicControls(horizontal: true);

            Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(
                LeftId,
                NowFocus.focusedResolvedId,
                "The deferred entry must seed the destination after its current " +
                "registry is committed.");
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
        }
    }

    [UnityTest]
    public IEnumerator DestinationSelectCallbackBeforeProxyKeepsExplicitFocus()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(RightId);

        GameObject destinationObject = new GameObject(
            "Destination Now Host",
            typeof(RectTransform));

        try
        {
            var focusOnSelect = destinationObject.AddComponent<FocusOnSelect>();
            focusOnSelect.id = RightId;
            var destinationGraphic =
                destinationObject.AddComponent<TestNowGraphic>();
            var destinationProxy =
                destinationObject.AddComponent<NowUGUINavigationProxy>();
            destinationGraphic.RebuildGeometryForTest();

            _provider.snapshot = NavigationSnapshot(frame: 1);

            using (NowInput.Begin(_provider, Surface))
            using (NowFocus.BeginHostRegistration(
                destinationGraphic.focusHostId,
                destinationProxy.focusAdapter))
            {
                NowFocus.Register(
                    destinationGraphic.ResolveControlId(201),
                    new NowRect(10f, 10f, 60f, 30f));
            }

            _eventSystem.SetSelectedGameObject(destinationObject);

            Assert.AreSame(
                destinationObject,
                _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(
                RightId,
                NowFocus.focusedResolvedId,
                "An earlier destination OnSelect handler's explicit focus " +
                "request must not be replaced by proxy entry.");

            yield return null;

            Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        }
        finally
        {
            Object.DestroyImmediate(destinationObject);
        }
    }

    [UnityTest]
    public IEnumerator FocusRequestedDuringExternalOnSelectAdoptsProxyNextFrame()
    {
        RegisterControls();
        var focusOnSelect = _outsideObject.AddComponent<FocusOnSelect>();
        focusOnSelect.id = LeftId;

        _eventSystem.SetSelectedGameObject(_outsideObject);

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);

        yield return null;

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);
    }

    [UnityTest]
    public IEnumerator FocusRequestedByBoundaryDestinationAdoptsProxyNextFrame()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(RightId);
        CommitGraphicControls();

        var focusOnSelect = _outsideObject.AddComponent<FocusOnSelect>();
        focusOnSelect.id = LeftId;

        _proxy.OnMove(Move(MoveDirection.Right));

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            LeftId,
            NowFocus.focusedResolvedId,
            "The destination's OnSelect focus request must win over source cleanup.");

        yield return null;

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);
    }

    [UnityTest]
    public IEnumerator FocusRequestedByTabDestinationAdoptsProxyNextFrame()
    {
        RegisterControls();
        _proxy.tabNext = _outside;
        SelectProxy();
        NowFocus.Focus(RightId);
        CommitGraphicControls(horizontal: true);

        var focusOnSelect = _outsideObject.AddComponent<FocusOnSelect>();
        focusOnSelect.id = LeftId;
        _provider.snapshot = NavigationSnapshot(next: true, frame: 2);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy.focusAdapter))
        {
            NowFocus.Register(LeftId, new NowRect(10f, 10f, 60f, 30f));
            NowFocus.Register(RightId, new NowRect(110f, 10f, 60f, 30f));
        }
        _proxy.ProcessPendingYield();

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(
            LeftId,
            NowFocus.focusedResolvedId,
            "The Tab destination's OnSelect focus request must win over source cleanup.");

        yield return null;

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void NullEventSystemSelectionPreservesFocusButExternalSelectionClearsIt()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        _eventSystem.SetSelectedGameObject(null);
        RegisterControls(frame: 2);

        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);
        Assert.IsTrue(_graphic.hasFocusedControl);

        _eventSystem.SetSelectedGameObject(_outsideObject);
        RegisterControls(frame: 3);

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
        Assert.IsFalse(_graphic.hasFocusedControl);
    }

    [Test]
    public void EmptyHostDoesNotTrapUGUINavigation()
    {
        RegisterEmptyHost();
        SelectProxy();
        _graphic.RebuildGeometryForTest();

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void MoveDirectionNoneIsNotConsumed()
    {
        RegisterControls();
        SelectProxy();

        AxisEventData move = Move(MoveDirection.None);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void SelectionBeforeFirstRegistrationSeedsWhenControlsAppear()
    {
        SelectProxy();
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);

        NowControlState.BeginRepaintTracking();
        RegisterControls();
        bool repaintRequested = NowControlState.EndRepaintTracking();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedResolvedId);
        Assert.IsTrue(repaintRequested);
    }

    [Test]
    public void DirectionalSelectionBeforeFirstRegistrationSeedsTheLastEdgeControl()
    {
        AxisEventData enterFromBelow = Move(MoveDirection.Up);
        _eventSystem.SetSelectedGameObject(_hostObject, enterFromBelow);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);

        RegisterThreeControls(MoveDirection.Down);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LastId, NowFocus.focusedResolvedId);
    }

    [Test]
    public void CollapsedHostCannotRouteIntoItsLastVisibleControls()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
        _graphic.RebuildGeometryForTest();

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
        Assert.IsFalse(_graphic.hasFocusedControl);

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void FailedRebuildCannotCommitPartiallyDrawnControls()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        _graphic.throwOnDraw = true;
        LogAssert.Expect(
            LogType.Exception,
            new Regex("Intentional proxy host failure"));
        _graphic.RebuildGeometryForTest();

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
        Assert.IsFalse(_graphic.hasFocusedControl);

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void FailedDirtyRebuildCannotResolveADeferredTabBoundary()
    {
        _proxy.tabNext = _outside;
        _graphic.drawTestControls = true;
        _graphic.inputProvider = _provider;

        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);

        _provider.snapshot = NavigationSnapshot(frame: 1);
        _graphic.RebuildGeometryForTest();
        SelectProxy();
        NowFocus.Focus(RightId);

        _provider.snapshot = NavigationSnapshot(next: true, frame: 2);
        _graphic.throwOnDraw = true;
        _graphic.MarkDirty();
        LogAssert.Expect(
            LogType.Exception,
            new Regex("Intentional proxy host failure"));
        _graphic.RebuildGeometryForTest();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void DisablingGraphicClearsItsHostFocusAndRegistry()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        _graphic.enabled = false;

        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
        Assert.IsFalse(_graphic.hasFocusedControl);

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void TabAtHostBoundaryUsesConfiguredUGUITarget()
    {
        RegisterControls();
        _proxy.tabNext = _outside;
        SelectProxy();
        NowFocus.Focus(RightId);
        CommitGraphicControls(horizontal: true);

        _provider.snapshot = NavigationSnapshot(next: true, frame: 2);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy.focusAdapter))
        {
            NowFocus.Register(LeftId, new NowRect(10f, 10f, 60f, 30f));
            NowFocus.Register(RightId, new NowRect(110f, 10f, 60f, 30f));
        }
        _proxy.ProcessPendingYield();

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void LastInternalControlIsSelectedBeforeTabYieldsToUGUI()
    {
        _proxy.tabNext = _outside;
        RegisterThreeControls(MoveDirection.Down, frame: 1);
        SelectProxy();
        CommitDirectionalGraphicControls(MoveDirection.Down);

        RegisterThreeControls(MoveDirection.Down, frame: 2, focusNext: true);

        Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);

        RegisterThreeControls(MoveDirection.Down, frame: 3, focusNext: true);

        Assert.AreEqual(LastId, NowFocus.focusedResolvedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);

        RegisterThreeControls(MoveDirection.Down, frame: 4, focusNext: true);
        _proxy.ProcessPendingYield();

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(NowResolvedId.None, NowFocus.focusedResolvedId);
    }

    [Test]
    public void ReverseTabIntoAnotherProxySeedsItsLastControl()
    {
        RegisterControls();

        GameObject sourceObject = new GameObject(
            "Source Now Host",
            typeof(RectTransform));

        try
        {
            var sourceGraphic = sourceObject.AddComponent<TestNowGraphic>();
            var sourceProxy = sourceObject.AddComponent<NowUGUINavigationProxy>();
            sourceProxy.tabPrevious = _proxy;
            sourceGraphic.RebuildGeometryForTest();

            _provider.snapshot = NavigationSnapshot(frame: 1);

            using (NowInput.Begin(_provider, Surface))
            using (NowFocus.BeginHostRegistration(
                sourceGraphic.focusHostId,
                sourceProxy.focusAdapter))
            {
                NowFocus.Register(
                    sourceGraphic.ResolveControlId(201),
                    new NowRect(10f, 10f, 60f, 30f));
            }

            _eventSystem.SetSelectedGameObject(sourceObject);
            Assert.AreEqual(
                sourceGraphic.ResolveControlId(201),
                NowFocus.focusedResolvedId);

            Assert.IsTrue(sourceProxy.TryYieldTab(-1));

            Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(RightId, NowFocus.focusedResolvedId);
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
        }
    }

    void RegisterControls(
        NowFocusNavigationLock leftLock = NowFocusNavigationLock.None,
        bool includeRight = true,
        int frame = 1)
    {
        _provider.snapshot = NavigationSnapshot(frame: frame);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy.focusAdapter))
        {
            NowFocus.Register(
                LeftId,
                new NowRect(10f, 10f, 60f, 30f),
                default,
                leftLock);

            if (includeRight)
                NowFocus.Register(RightId, new NowRect(110f, 10f, 60f, 30f));
        }
    }

    void RegisterThreeControls(
        MoveDirection direction,
        bool staggerHorizontally = false,
        int frame = 1,
        bool focusNext = false)
    {
        _provider.snapshot = NavigationSnapshot(next: focusNext, frame: frame);

        bool horizontal = direction == MoveDirection.Left ||
            direction == MoveDirection.Right;

        bool reverse = direction == MoveDirection.Left ||
            direction == MoveDirection.Up;
        float firstAlong = reverse ? 110f : 10f;
        float lastAlong = reverse ? 10f : 110f;

        NowRect first = horizontal
            ? new NowRect(reverse ? 210f : 10f, 10f, 60f, 30f)
            : new NowRect(staggerHorizontally ? 210f : 10f, firstAlong, 60f, 30f);
        NowRect middle = horizontal
            ? new NowRect(110f, 10f, 60f, 30f)
            : new NowRect(staggerHorizontally ? 110f : 10f, 60f, 60f, 30f);
        NowRect last = horizontal
            ? new NowRect(reverse ? 10f : 210f, 10f, 60f, 30f)
            : new NowRect(10f, lastAlong, 60f, 30f);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy.focusAdapter))
        {
            NowFocus.Register(LeftId, first);
            NowFocus.Register(RightId, middle);
            NowFocus.Register(LastId, last);
        }
    }

    void ConfigureOutsideTarget(MoveDirection direction)
    {
        var navigation = Navigation.defaultNavigation;
        navigation.mode = Navigation.Mode.Explicit;

        switch (direction)
        {
            case MoveDirection.Left:
                navigation.selectOnLeft = _outside;
                break;
            case MoveDirection.Right:
                navigation.selectOnRight = _outside;
                break;
            case MoveDirection.Up:
                navigation.selectOnUp = _outside;
                break;
            case MoveDirection.Down:
                navigation.selectOnDown = _outside;
                break;
        }

        _proxy.navigation = navigation;
    }

    void CommitGraphicControls(
        bool includeLast = false,
        bool horizontal = false,
        bool includeFirst = true,
        bool includeMiddle = true)
    {
        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);
        _graphic.drawTestControls = true;
        _graphic.horizontalTestControls = horizontal;
        _graphic.includeFirstControl = includeFirst;
        _graphic.includeMiddleControl = includeMiddle;
        _graphic.includeLastControl = includeLast;
        _graphic.RebuildGeometryForTest();
    }

    void CommitDirectionalGraphicControls(
        MoveDirection direction,
        bool staggerHorizontally = false)
    {
        RectTransform rect = _hostObject.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Surface.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Surface.y);
        _graphic.drawTestControls = true;
        _graphic.directionalTestLayout = true;
        _graphic.directionalTestMove = direction;
        _graphic.staggerDirectionalTestLayout = staggerHorizontally;
        _graphic.includeFirstControl = true;
        _graphic.includeMiddleControl = true;
        _graphic.includeLastControl = true;
        _graphic.RebuildGeometryForTest();
    }

    void RegisterEmptyHost()
    {
        _provider.snapshot = NavigationSnapshot(frame: 1);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy.focusAdapter))
        {
        }
    }

    void SelectProxy()
    {
        _eventSystem.SetSelectedGameObject(_hostObject);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
    }

    AxisEventData Move(MoveDirection direction)
    {
        return new AxisEventData(_eventSystem)
        {
            moveDir = direction,
            moveVector = direction switch
            {
                MoveDirection.Left => Vector2.left,
                MoveDirection.Right => Vector2.right,
                MoveDirection.Up => Vector2.up,
                MoveDirection.Down => Vector2.down,
                _ => Vector2.zero
            }
        };
    }

    static NowInputSnapshot NavigationSnapshot(
        bool previous = false,
        bool next = false,
        Vector2 navigation = default,
        int frame = 1)
    {
        return new NowInputSnapshot(
            false, default, default, default,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            default, navigation,
            previous, next,
            false, false, false, false, false, false,
            frame, frame);
    }
}
#endif
