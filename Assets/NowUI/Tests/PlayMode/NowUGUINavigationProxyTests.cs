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
    const int LeftId = 101;
    const int RightId = 102;

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

        public void RebuildGeometryForTest()
        {
            UpdateGeometry();
        }

        protected override void DrawNowUI(NowRect rect)
        {
            if (throwOnDraw)
                throw new InvalidOperationException("Intentional proxy host failure.");
        }
    }

    sealed class FocusOnSelect : MonoBehaviour, ISelectHandler
    {
        public int id;

        public void OnSelect(BaseEventData eventData)
        {
            NowFocus.Focus(id);
        }
    }

    readonly FakeProvider _provider = new FakeProvider();

    GameObject _eventSystemObject;
    GameObject _hostObject;
    GameObject _outsideObject;
    EventSystem _eventSystem;
    TestNowGraphic _graphic;
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
        Assert.AreEqual(LeftId, NowFocus.focusedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void MoveStaysInsideProxyWhileNowUIHasATarget()
    {
        RegisterControls();
        SelectProxy();

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsTrue(move.used);
        Assert.AreEqual(RightId, NowFocus.focusedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
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
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy))
        {
            NowFocus.Register(LeftId, new NowRect(10f, 10f, 60f, 30f));
            NowFocus.Register(RightId, new NowRect(110f, 10f, 60f, 30f));
        }

        Assert.AreEqual(RightId, NowFocus.focusedId);
        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void BoundaryMoveYieldsToConfiguredUGUISelectable()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(RightId);

        _proxy.OnMove(Move(MoveDirection.Right));

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(0, NowFocus.focusedId);
    }

    [Test]
    public void InboundMoveSeedsTheMatchingNowUIEdge()
    {
        RegisterControls();
        _eventSystem.SetSelectedGameObject(_outsideObject);

        AxisEventData enterFromRight = Move(MoveDirection.Left);
        _eventSystem.SetSelectedGameObject(_hostObject, enterFromRight);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(RightId, NowFocus.focusedId);
    }

    [Test]
    public void ProgrammaticNowUIFocusSelectsItsProxy()
    {
        RegisterControls();
        _eventSystem.SetSelectedGameObject(_outsideObject);

        NowFocus.Focus(LeftId);

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedId);

        NowFocus.Focus(RightId);
        _proxy.OnMove(Move(MoveDirection.Right));

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(0, NowFocus.focusedId);
    }

    [UnityTest]
    public IEnumerator FocusRequestedDuringExternalOnSelectAdoptsProxyNextFrame()
    {
        RegisterControls();
        var focusOnSelect = _outsideObject.AddComponent<FocusOnSelect>();
        focusOnSelect.id = LeftId;

        _eventSystem.SetSelectedGameObject(_outsideObject);

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedId);

        yield return null;

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedId);
    }

    [Test]
    public void NullEventSystemSelectionPreservesFocusButExternalSelectionClearsIt()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        _eventSystem.SetSelectedGameObject(null);
        RegisterControls(frame: 2);

        Assert.AreEqual(LeftId, NowFocus.focusedId);
        Assert.IsTrue(_graphic.hasFocusedControl);

        _eventSystem.SetSelectedGameObject(_outsideObject);
        RegisterControls(frame: 3);

        Assert.AreEqual(0, NowFocus.focusedId);
        Assert.IsFalse(_graphic.hasFocusedControl);
    }

    [Test]
    public void EmptyHostDoesNotTrapUGUINavigation()
    {
        RegisterEmptyHost();
        SelectProxy();

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(0, NowFocus.focusedId);
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
        Assert.AreEqual(LeftId, NowFocus.focusedId);
    }

    [Test]
    public void SelectionBeforeFirstRegistrationSeedsWhenControlsAppear()
    {
        SelectProxy();
        Assert.AreEqual(0, NowFocus.focusedId);

        NowControlState.BeginRepaintTracking();
        RegisterControls();
        bool repaintRequested = NowControlState.EndRepaintTracking();

        Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(LeftId, NowFocus.focusedId);
        Assert.IsTrue(repaintRequested);
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

        Assert.AreEqual(0, NowFocus.focusedId);
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

        Assert.AreEqual(0, NowFocus.focusedId);
        Assert.IsFalse(_graphic.hasFocusedControl);

        AxisEventData move = Move(MoveDirection.Right);
        _proxy.OnMove(move);

        Assert.IsFalse(move.used);
        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
    }

    [Test]
    public void DisablingGraphicClearsItsHostFocusAndRegistry()
    {
        RegisterControls();
        SelectProxy();
        NowFocus.Focus(LeftId);

        _graphic.enabled = false;

        Assert.AreEqual(0, NowFocus.focusedId);
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

        _provider.snapshot = NavigationSnapshot(next: true, frame: 2);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy))
        {
            NowFocus.Register(LeftId, new NowRect(10f, 10f, 60f, 30f));
            NowFocus.Register(RightId, new NowRect(110f, 10f, 60f, 30f));
        }

        Assert.AreSame(_outsideObject, _eventSystem.currentSelectedGameObject);
        Assert.AreEqual(0, NowFocus.focusedId);
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

            _provider.snapshot = NavigationSnapshot(frame: 1);

            using (NowInput.Begin(_provider, Surface))
            using (NowFocus.BeginHostRegistration(
                sourceGraphic.focusHostId,
                sourceProxy))
            {
                NowFocus.Register(201, new NowRect(10f, 10f, 60f, 30f));
            }

            _eventSystem.SetSelectedGameObject(sourceObject);
            Assert.AreEqual(201, NowFocus.focusedId);

            Assert.IsTrue(sourceProxy.TryYieldTab(-1));

            Assert.AreSame(_hostObject, _eventSystem.currentSelectedGameObject);
            Assert.AreEqual(RightId, NowFocus.focusedId);
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
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy))
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

    void RegisterEmptyHost()
    {
        _provider.snapshot = NavigationSnapshot(frame: 1);

        using (NowInput.Begin(_provider, Surface))
        using (NowFocus.BeginHostRegistration(_graphic.focusHostId, _proxy))
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
