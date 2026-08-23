using System;
using System.Reflection;
using NUnit.Framework;
using NowUI;
using UnityEngine;

public class NowGUIFocusHostIsolationTests
{
    const int SharedNativeControlId = 7101;

    static readonly Rect PanelRect = new Rect(0f, 0f, 240f, 80f);

    static readonly NowInputSurface Surface =
        new NowInputSurface(new Vector2(PanelRect.width, PanelRect.height), PanelRect);

    static readonly MethodInfo GetEntryMethod = typeof(NowGUI).GetMethod(
        "GetEntry",
        BindingFlags.NonPublic | BindingFlags.Static);

    static readonly FieldInfo InputSnapshotField = typeof(NowInput).GetField(
        "_snapshot",
        BindingFlags.NonPublic | BindingFlags.Static);

    static readonly MethodInfo CleanupUnusedEntriesMethod = typeof(NowGUI).GetMethod(
        "CleanupUnusedEntries",
        BindingFlags.NonPublic | BindingFlags.Static);

    static readonly MethodInfo CleanupUnusedEntriesForActiveContextMethod =
        typeof(NowGUI).GetMethod(
            "CleanupUnusedEntriesForActiveContext",
            BindingFlags.NonPublic | BindingFlags.Static);

    static readonly FieldInfo LastCleanupTimeField = typeof(NowGUI).GetField(
        "_lastCleanupTime",
        BindingFlags.NonPublic | BindingFlags.Static);

    static readonly FieldInfo RemoveKeysField = typeof(NowGUI).GetField(
        "_removeKeys",
        BindingFlags.NonPublic | BindingFlags.Static);

    Event _previousEvent;

    bool _previousRespectEventSystem;

    bool _previousGUIChanged;

    Action _previousRepaintRequested;

    Action<NowIMGUIInputProvider> _previousHostRepaintRequested;

    Action<NowIMGUIInputProvider, float> _previousHostRepaintAfterRequested;

    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    struct PanelPass
    {
        public NowGUI.CacheEntry entry;
        public FakeProvider provider;
        public NowResolvedId firstId;
        public NowResolvedId secondId;
        public int frame;
        public int inputPass;
    }

    [SetUp]
    public void SetUp()
    {
        _previousEvent = Event.current;
        _previousRespectEventSystem = NowFocus.respectEventSystem;
        _previousGUIChanged = GUI.changed;
        _previousRepaintRequested = NowIMGUIInputProvider.repaintRequested;
        _previousHostRepaintRequested = NowIMGUIInputProvider.hostRepaintRequested;
        _previousHostRepaintAfterRequested = NowIMGUIInputProvider.hostRepaintAfterRequested;

        NowGUI.DisposeAll();
        NowInput.Reset();
        NowFocus.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowControlState.Reset();

        Event.current = null;
        GUI.changed = false;
        NowFocus.respectEventSystem = false;
        NowIMGUIInputProvider.repaintRequested = () => { };
        NowIMGUIInputProvider.hostRepaintRequested = _ => { };
        NowIMGUIInputProvider.hostRepaintAfterRequested = (_, _) => { };
    }

    [TearDown]
    public void TearDown()
    {
        NowGUI.DisposeAll();
        NowInput.Reset();
        NowFocus.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowControlState.Reset();

        Event.current = _previousEvent;
        GUI.changed = _previousGUIChanged;
        NowFocus.respectEventSystem = _previousRespectEventSystem;
        NowIMGUIInputProvider.repaintRequested = _previousRepaintRequested;
        NowIMGUIInputProvider.hostRepaintRequested = _previousHostRepaintRequested;
        NowIMGUIInputProvider.hostRepaintAfterRequested = _previousHostRepaintAfterRequested;
    }

    [Test]
    public void ContextsWithSameNativeControlIdKeepIndependentFocusAndTabRegistries()
    {
        var firstContext = new object();
        var secondContext = new object();

        PanelPass first = CreatePanel(firstContext);
        PanelPass second = CreatePanel(secondContext);

        Assert.AreNotEqual(first.firstId, second.firstId);
        Assert.AreNotEqual(first.secondId, second.secondId);
        Assert.AreNotSame(first.entry, second.entry);
        Assert.AreNotEqual(first.entry.focusHostId, second.entry.focusHostId);

        DrawHostPass(ref first, Snapshot(frame: 700, inputPass: 10));
        DrawHostPass(ref second, Snapshot(frame: 700, inputPass: 10));

        NowFocus.Focus(first.firstId);

        DrawHostPass(ref second, Snapshot(frame: 700, inputPass: 11, focusNext: true));
        Assert.AreEqual(
            first.firstId,
            NowFocus.focusedResolvedId,
            "A Tab pass in another GUI context must not navigate the focused host.");

        DrawHostPass(ref first, Snapshot(frame: 700, inputPass: 11, focusNext: true));
        Assert.AreEqual(
            first.secondId,
            NowFocus.focusedResolvedId,
            "The focused GUI context must navigate within its own retained registry.");

        NowFocus.Focus(second.firstId);
        DrawHostPass(ref second, Snapshot(frame: 700, inputPass: 12, focusNext: true));

        Assert.AreEqual(
            second.secondId,
            NowFocus.focusedResolvedId,
            "The second GUI context must retain a separate Tab registry even when the native control id matches.");
    }

    [Test]
    public void HostProcessesTabOnALaterInputPassWithinTheSameUnityFrame()
    {
        var context = new object();
        PanelPass panel = CreatePanel(context);
        PanelPass registered = DrawHostPass(
            ref panel,
            Snapshot(frame: 900, inputPass: 20));

        NowFocus.Focus(registered.firstId);
        PanelPass navigated = DrawHostPass(
            ref panel,
            Snapshot(frame: 900, inputPass: 21, focusNext: true));

        Assert.AreEqual(
            registered.frame,
            navigated.frame,
            "The regression requires Layout and KeyDown to occur in one Unity frame.");
        Assert.Greater(
            navigated.inputPass,
            registered.inputPass,
            "Each native IMGUI pass must receive a distinct input identity.");
        Assert.AreEqual(
            registered.secondId,
            NowFocus.focusedResolvedId,
            "Tab must be processed by the host scope even after an earlier IMGUI pass in the same Unity frame.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeTabIsConsumedAndRepaintsOnlyAfterTheHostHandlesIt(bool shift)
    {
        var panel = CreatePanel(new object(), 7201);
        NowResolvedId startingId = shift ? panel.secondId : panel.firstId;
        NowResolvedId expectedId = shift ? panel.firstId : panel.secondId;
        NowFocus.Focus(startingId);
        var expectedProvider = panel.entry.inputProvider;
        int repaintCount = 0;
        NowIMGUIInputProvider.hostRepaintRequested = provider =>
        {
            Assert.AreSame(
                expectedProvider,
                provider,
                "The host that handled Tab must own the repaint request.");
            ++repaintCount;
        };
        GUI.changed = false;
        var tabEvent = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Tab,
            shift = shift,
            mousePosition = new Vector2(20f, 20f)
        };

        DrawIMGUIHostPass(
            ref panel,
            tabEvent,
            EventType.Ignore);

        Assert.AreEqual(expectedId, NowFocus.focusedResolvedId);
        Assert.AreEqual(
            EventType.Used,
            tabEvent.type,
            "Tab becomes native-consumed only after the owning focus host handles it.");
        Assert.IsTrue(
            GUI.changed,
            "Handled Tab navigation must repaint focus visuals immediately.");
        Assert.AreEqual(1, repaintCount);
    }

    [Test]
    public void OrderedPanelsInOneContextRouteOneNativeTabEventToTheFocusedHost()
    {
        var context = new object();
        var first = CreatePanel(context, 7211);
        var second = CreatePanel(context, 7212);

        Assert.AreNotSame(first.entry, second.entry);
        Assert.AreNotEqual(first.entry.focusHostId, second.entry.focusHostId);
        NowFocus.Focus(second.firstId);

        int firstRepaints = 0;
        int secondRepaints = 0;
        var firstProvider = first.entry.inputProvider;
        var secondProvider = second.entry.inputProvider;
        NowIMGUIInputProvider.hostRepaintRequested = provider =>
        {
            if (ReferenceEquals(provider, firstProvider))
                ++firstRepaints;
            else if (ReferenceEquals(provider, secondProvider))
                ++secondRepaints;
            else
                Assert.Fail("An unrelated provider requested a repaint during the two-panel Tab replay.");
        };
        GUI.changed = false;
        var tabEvent = new Event
        {
            type = EventType.KeyDown,
            keyCode = KeyCode.Tab,
            mousePosition = new Vector2(20f, 20f)
        };

        DrawIMGUIHostPass(
            ref first,
            tabEvent,
            EventType.Ignore);

        Assert.AreEqual(
            EventType.KeyDown,
            tabEvent.type,
            "An earlier panel that does not own focus must leave the native Tab available.");
        Assert.AreEqual(second.firstId, NowFocus.focusedResolvedId);
        Assert.AreEqual(0, firstRepaints);
        Assert.AreEqual(0, secondRepaints);

        DrawIMGUIHostPass(
            ref second,
            tabEvent,
            EventType.Ignore);

        Assert.AreEqual(second.secondId, NowFocus.focusedResolvedId);
        Assert.AreEqual(EventType.Used, tabEvent.type);
        Assert.AreEqual(0, firstRepaints);
        Assert.AreEqual(
            1,
            secondRepaints,
            "Only the focused panel may consume Tab and request its host repaint.");
        Assert.IsTrue(GUI.changed);
    }

    [Test]
    public void ContextFocusLossClearsHeldKeyAndCapturedPointerBeforeRefocus()
    {
        const int HostControlId = 7251;
        const int NowControlId = 7252;
        var context = new object();
        NowGUI.CacheEntry entry = GetEntry(context, HostControlId);
        Event previousEvent = Event.current;
        int previousHotControl = GUIUtility.hotControl;

        (NowInteraction interaction, NowInputSnapshot snapshot) Draw(
            Event inputEvent,
            EventType routedType,
            bool ownsCapture)
        {
            Event.current = null;

            using (NowGUI.AutoForEvent(
                       context,
                       HostControlId,
                       PanelRect,
                       Color.clear,
                       1f,
                       repaint: false,
                       hostFocused: true,
                       trackInputRepaint: true))
            {
                Assert.NotNull(InputSnapshotField);
                Assert.IsTrue(entry.inputProvider.TryGetSnapshot(
                    Surface,
                    inputEvent,
                    routedType,
                    ownsCapture,
                    out var snapshot));
                InputSnapshotField.SetValue(null, snapshot);
                NowResolvedId controlId = NowControls.GetControlId(new NowId(NowControlId));
                var interaction = NowInput.Interact(
                    controlId,
                    new NowRect(10f, 10f, 80f, 24f));
                return (interaction, snapshot);
            }
        }

        try
        {
            GUIUtility.hotControl = 0;
            var pressed = Draw(
                new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = new Vector2(20f, 20f)
                },
                EventType.MouseDown,
                ownsCapture: false);

            Assert.IsTrue(pressed.interaction.pressed);
            Assert.AreEqual(HostControlId, GUIUtility.hotControl);

            var keyDown = Draw(
                new Event
                {
                    type = EventType.KeyDown,
                    keyCode = KeyCode.Return,
                    mousePosition = new Vector2(20f, 20f)
                },
                EventType.KeyDown,
                ownsCapture: true);

            Assert.IsTrue(keyDown.snapshot.submitDown);
            Assert.IsTrue(keyDown.interaction.held);

            NowGUI.NotifyContextFocus(
                context,
                focused: false,
                releaseNativeCapture: false);
            NowGUI.NotifyContextFocus(
                context,
                focused: true,
                releaseNativeCapture: false);

            var resumed = Draw(
                new Event
                {
                    type = EventType.Layout,
                    mousePosition = new Vector2(20f, 20f)
                },
                EventType.Layout,
                ownsCapture: true);

            Assert.IsTrue(
                resumed.snapshot.pointerCaptureCancelled,
                "The first pass after refocus must report the gesture that was abandoned while unfocused.");
            Assert.IsFalse(
                resumed.snapshot.submitDown,
                "A missed KeyUp while unfocused must not leave submit latched.");
            Assert.AreEqual(Vector2.zero, resumed.snapshot.navigation);
            Assert.IsTrue(resumed.interaction.cancelled);
            Assert.IsFalse(resumed.interaction.clicked);
            Assert.AreEqual(NowResolvedId.None, NowInput.activeId);
            Assert.AreEqual(
                0,
                GUIUtility.hotControl,
                "The resumed host pass must release deferred native capture in its own GUI context.");
        }
        finally
        {
            entry.inputProvider.ResetState(releaseNativeCapture: false);
            GUIUtility.hotControl = previousHotControl;
            Event.current = previousEvent;
        }
    }

    [Test]
    public void CleanupKeepsLiveUnityHostAndRemovesItAfterDestruction()
    {
        var context = ScriptableObject.CreateInstance<NowGUIFocusHostTestContext>();
        const int firstControlId = 7301;
        const int secondControlId = 7302;
        NowGUI.CacheEntry firstEntry = GetEntry(context, firstControlId);
        NowGUI.CacheEntry secondEntry = GetEntry(context, secondControlId);
        firstEntry.lastUsedTime = double.NegativeInfinity;
        secondEntry.lastUsedTime = double.NegativeInfinity;

        ForceCacheCleanup();

        Assert.AreSame(
            firstEntry,
            GetEntry(context, firstControlId),
            "An idle live Unity host must not lose retained panel state merely because another host triggers cache cleanup.");
        Assert.AreSame(
            secondEntry,
            GetEntry(context, secondControlId),
            "Cache cleanup outside an idle live context must preserve all of that context's control IDs.");

        UnityEngine.Object.DestroyImmediate(context);
        ForceCacheCleanup();

        Assert.AreNotSame(
            firstEntry,
            GetEntry(context, firstControlId),
            "Destroyed Unity hosts must be removed deterministically on the next cache cleanup.");
        Assert.AreNotSame(secondEntry, GetEntry(context, secondControlId));
        Assert.AreEqual(
            0,
            ((System.Collections.ICollection)RemoveKeysField.GetValue(null)).Count,
            "Cache cleanup scratch must not retain destroyed host references.");
    }

    [Test]
    public void ActiveUnityContextEvictsExpiredSiblingControlIdAndDisposesItsResources()
    {
        var context = ScriptableObject.CreateInstance<NowGUIFocusHostTestContext>();
        const int activeControlId = 7311;
        const int staleControlId = 7312;

        try
        {
            NowGUI.CacheEntry activeEntry = GetEntry(context, activeControlId);
            NowGUI.CacheEntry staleEntry = GetEntry(context, staleControlId);
            RenderTexture activeTarget = activeEntry.GetTarget(8, 8);
            RenderTexture staleTarget = staleEntry.GetTarget(8, 8);
            activeEntry.lastUsedTime = double.NegativeInfinity;
            staleEntry.lastUsedTime = double.NegativeInfinity;
            activeEntry.contextActivity.lastUsedTime = double.NegativeInfinity;

            using (NowGUI.AutoForEvent(
                context,
                activeControlId,
                PanelRect,
                Color.clear,
                1f,
                repaint: false))
            {
            }

            ForceCacheCleanup(context);

            Assert.AreSame(
                activeEntry,
                GetEntry(context, activeControlId),
                "The control ID observed by the active context must retain its cached state.");
            Assert.AreSame(
                activeTarget,
                activeEntry.target,
                "Cleaning an obsolete sibling must not dispose the active panel's render target.");
            Assert.AreSame(
                staleEntry,
                GetEntry(context, staleControlId),
                "The first pass after an idle context resumes must preserve later-drawn sibling panels.");
            Assert.AreSame(staleTarget, staleEntry.target);

            activeEntry.contextActivity.cleanupEligibleTime =
                double.NegativeInfinity;
            ForceCacheCleanup(context);

            Assert.AreNotSame(
                staleEntry,
                GetEntry(context, staleControlId),
                "An expired sibling control ID must be reclaimed after its context remains active through the resume grace.");
            Assert.IsNull(
                staleEntry.target,
                "Evicting a sibling cache entry must release its render target.");
            Assert.Throws<ObjectDisposedException>(
                () => staleEntry.renderer.Clear(),
                "Evicting a sibling cache entry must dispose its renderer.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(context);
        }
    }

    static PanelPass CreatePanel(
        object context,
        int nativeControlId = SharedNativeControlId)
    {
        using (NowGUI.AutoForEvent(
            context,
            nativeControlId,
            PanelRect,
            Color.clear,
            1f,
            repaint: false))
        {
            NowResolvedId firstId = NowControls.GetControlId("shared-first");
            NowResolvedId secondId = NowControls.GetControlId("shared-second");

            NowFocus.Register(firstId, new NowRect(10f, 10f, 80f, 24f));
            NowFocus.Register(secondId, new NowRect(110f, 10f, 80f, 24f));

            return new PanelPass
            {
                entry = GetEntry(context, nativeControlId),
                provider = new FakeProvider(),
                firstId = firstId,
                secondId = secondId
            };
        }
    }

    static PanelPass DrawHostPass(ref PanelPass panel, NowInputSnapshot snapshot)
    {
        panel.provider.snapshot = snapshot;

        using (NowInput.Begin(panel.provider, Surface))
        using (NowFocus.BeginHostRegistration(panel.entry.focusHostId, null))
        using (NowControls.RestoreIdScope(panel.entry.scopeId))
        {
            NowResolvedId firstId = NowControls.GetControlId("shared-first");
            NowResolvedId secondId = NowControls.GetControlId("shared-second");

            Assert.AreEqual(panel.firstId, firstId);
            Assert.AreEqual(panel.secondId, secondId);

            NowFocus.Register(firstId, new NowRect(10f, 10f, 80f, 24f));
            NowFocus.Register(secondId, new NowRect(110f, 10f, 80f, 24f));

            panel.frame = NowInput.current.frame;
            panel.inputPass = NowInput.current.inputPass;
            return panel;
        }
    }

    static void DrawIMGUIHostPass(
        ref PanelPass panel,
        Event inputEvent,
        EventType routedType)
    {
        Event previousEvent = Event.current;
        Event.current = null;

        try
        {
            using (NowInput.Begin(panel.entry.inputProvider, Surface))
            {
                Assert.NotNull(InputSnapshotField, "NowInput snapshot test seam was not found.");
                Assert.IsTrue(panel.entry.inputProvider.TryGetSnapshot(
                    Surface,
                    inputEvent,
                    routedType,
                    ownsCapture: false,
                    out var snapshot));
                InputSnapshotField.SetValue(null, snapshot);

                using (NowFocus.BeginHostRegistration(panel.entry.focusHostId, null))
                using (NowControls.RestoreIdScope(panel.entry.scopeId))
                {
                    NowResolvedId firstId = NowControls.GetControlId("shared-first");
                    NowResolvedId secondId = NowControls.GetControlId("shared-second");

                    Assert.AreEqual(panel.firstId, firstId);
                    Assert.AreEqual(panel.secondId, secondId);

                    NowFocus.Register(firstId, new NowRect(10f, 10f, 80f, 24f));
                    NowFocus.Register(secondId, new NowRect(110f, 10f, 80f, 24f));

                    panel.frame = NowInput.current.frame;
                    panel.inputPass = NowInput.current.inputPass;
                }
            }
        }
        finally
        {
            Event.current = previousEvent;
        }
    }

    static NowGUI.CacheEntry GetEntry(object context, int nativeControlId = SharedNativeControlId)
    {
        Assert.NotNull(GetEntryMethod, "NowGUI cache lookup seam was not found.");
        return (NowGUI.CacheEntry)GetEntryMethod.Invoke(
            null,
            new object[] { context, nativeControlId });
    }

    static void ForceCacheCleanup()
    {
        Assert.NotNull(CleanupUnusedEntriesMethod);
        Assert.NotNull(LastCleanupTimeField);
        Assert.NotNull(RemoveKeysField);
        LastCleanupTimeField.SetValue(null, double.NegativeInfinity);
        CleanupUnusedEntriesMethod.Invoke(null, null);
    }

    static void ForceCacheCleanup(object activeContext)
    {
        Assert.NotNull(CleanupUnusedEntriesForActiveContextMethod);
        Assert.NotNull(LastCleanupTimeField);
        Assert.NotNull(RemoveKeysField);
        LastCleanupTimeField.SetValue(null, double.NegativeInfinity);
        CleanupUnusedEntriesForActiveContextMethod.Invoke(
            null,
            new[] { activeContext });
    }

    static NowInputSnapshot Snapshot(int frame, int inputPass, bool focusNext = false)
    {
        return new NowInputSnapshot(
            false,
            default,
            default,
            default,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            default,
            default,
            false,
            focusNext,
            false,
            false,
            false,
            false,
            false,
            false,
            frame,
            1f)
        {
            inputPass = inputPass
        };
    }
}

sealed class NowGUIFocusHostTestContext : ScriptableObject
{
}
