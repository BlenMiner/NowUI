using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using NowUI;

/// <summary>
/// Popup and overlay UX tests: modal outside-press consumption, the combo box
/// filter staying typable on the popup's focus layer, dropdown keyboard
/// driving, one-press-one-layer dismissal for nested overlays, and key-capture
/// cancel consumption — driven frame by frame with
/// <c>NowOverlay.ForceNewFrame</c> so the one-frame-late pointer blocks engage.
/// </summary>
public class NowPopupUXTests
{
    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class FakeKeyboard : INowTextInputSource
    {
        public NowTextInputFrame frame;

        public bool TryGetFrame(out NowTextInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    sealed class FakeKeys : INowKeyInputSource
    {
        public NowKeyInputFrame frame;

        public bool TryGetFrame(out NowKeyInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(600, 600);
    static readonly NowRect FieldRect = new NowRect(20, 20, 160, 30);
    static readonly NowRect ButtonRect = new NowRect(20, 320, 140, 32);
    static readonly NowRect ViewPopupRect = new NowRect(80f, 40f, 220f, 160f);
    static readonly List<string> Options = new List<string> { "Low", "Medium", "High" };

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    FakeKeys _keys;
    NowDrawList _drawList;
    int _frame;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowTextInput.Reset();
        NowKeyInput.Reset();
        NowLayout.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        _keys = new FakeKeys();
        NowTextInput.source = _keyboard;
        NowKeyInput.source = _keys;
        _drawList = new NowDrawList();
        _frame = 10;
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowKeyInput.Reset();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
    }

    NowInputSnapshot Snapshot(
        Vector2 position,
        bool down = false,
        bool pressed = false,
        bool released = false,
        Vector2 navigation = default,
        bool submitPressed = false,
        bool cancelPressed = false)
    {
        ++_frame;

        return new NowInputSnapshot(
            true, position, position, Vector2.zero,
            NowInputSnapshot.ToButtonMask(down, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(pressed, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(released, NowPointerButton.Primary),
            Vector2.zero, navigation,
            false, false,
            submitPressed, submitPressed, false,
            cancelPressed, cancelPressed, false,
            _frame, _frame * 0.25f);
    }

    int ResolveControlId(string id)
    {
        using (NowInput.Begin(_pointer, Surface))
            return NowControls.GetControlId(id);
    }

    bool DrawComboFrame(ref int selected, NowInputSnapshot snapshot, string typed = null)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keyboard.frame = new NowTextInputFrame { characters = typed };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.ComboBox(FieldRect, Options).SetId("combo").Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    bool DrawComboStringFrame(ref string selected, NowInputSnapshot snapshot, string typed = null)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keyboard.frame = new NowTextInputFrame { characters = typed };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.ComboBox(FieldRect, Options)
                .SetId("combo-string")
                .SetAllowCustomValue()
                .Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    [Test]
    public void ComboBoxFilterStaysTypableWhileThePopupBlockIsActive()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;

        DrawComboFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawComboFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawComboFrame(ref selected, Snapshot(fieldCenter));
        DrawComboFrame(ref selected, Snapshot(fieldCenter));

        DrawComboFrame(ref selected, Snapshot(fieldCenter), typed: "med");
        DrawComboFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(
            DrawComboFrame(ref selected, Snapshot(fieldCenter)),
            "Typing on the 3rd+ open frame must reach the filter and submit its first match.");
        Assert.AreEqual(1, selected, "The filter 'med' must select Medium.");
    }

    [Test]
    public void ComboBoxStringModeCanCommitCustomText()
    {
        string selected = string.Empty;
        var fieldCenter = FieldRect.center;

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter), typed: "Custom.Method");
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(
            DrawComboStringFrame(ref selected, Snapshot(fieldCenter)),
            "String combo boxes with custom values must apply the committed filter on the next Draw.");
        Assert.AreEqual("Custom.Method", selected);
    }

    [Test]
    public void ComboBoxStringModePrefersOptionMatchesBeforeCustomText()
    {
        string selected = string.Empty;
        var fieldCenter = FieldRect.center;

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter));

        DrawComboStringFrame(ref selected, Snapshot(fieldCenter), typed: "med");
        DrawComboStringFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(DrawComboStringFrame(ref selected, Snapshot(fieldCenter)));
        Assert.AreEqual("Medium", selected);
    }

    bool DrawDropdownAndButtonFrame(ref int selected, NowInputSnapshot snapshot)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        bool buttonClicked;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Now.Dropdown(FieldRect, "dd", Options).Draw(ref selected);
            buttonClicked = Now.Button(ButtonRect, "Outside").SetId("btn").Draw();
            NowOverlay.Flush();
        }

        return buttonClicked;
    }

    [Test]
    public void DropdownOutsidePressDismissesWithoutActivatingTheControlBeneath()
    {
        int selected = 0;
        var buttonCenter = ButtonRect.center;

        Assert.IsFalse(DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, down: true, pressed: true)));
        Assert.IsTrue(
            DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, released: true)),
            "With no popup open, the button must click.");

        int dropdownId = ResolveControlId("dd");
        NowControlState.Get<bool>(dropdownId) = true;

        DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter));
        DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter));

        bool pressClicked = DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, down: true, pressed: true));
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "The outside press must dismiss the popup.");

        bool releaseClicked = DrawDropdownAndButtonFrame(ref selected, Snapshot(buttonCenter, released: true));

        Assert.IsFalse(pressClicked || releaseClicked, "The dismissing press must not activate the button beneath.");
    }

    [Test]
    public void DropdownFieldPressWhileOpenClosesWithoutReopening()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;
        int dropdownId = ResolveControlId("dd");

        NowControlState.Get<bool>(dropdownId) = true;
        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter));
        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter));

        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "Pressing the field while open must close the popup.");

        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter, released: true));
        DrawDropdownAndButtonFrame(ref selected, Snapshot(fieldCenter));

        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "The release must not toggle the popup back open.");
    }

    bool DrawDropdownFrame(ref int selected, NowInputSnapshot snapshot)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keyboard.frame = default;
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.Dropdown(FieldRect, "dd", Options).Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    [Test]
    public void CurrentPassPopupOwnsWheelBeforeDeferredContentFlushes()
    {
        var popup = new NowRect(20f, 40f, 180f, 120f);
        var parent = new NowRect(0f, 0f, 260f, 220f);
        Vector2 popupWheel = default;
        _pointer.snapshot = new NowInputSnapshot(
            true,
            new Vector2(80f, 80f),
            new Vector2(80f, 80f),
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            new Vector2(0f, -1f),
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            77,
            1f);

        using (NowInput.Begin(_pointer, Surface))
        {
            NowOverlay.BlockAllSurfaces(7001);
            NowOverlay.DeferScreen(popup, 7001, _ =>
            {
                popupWheel = NowInput.ConsumeScrollDelta(popup);
            });

            Assert.AreEqual(
                Vector2.zero,
                NowInput.ConsumeScrollDelta(parent),
                "An enclosing scroll view must stand down for a popup declared earlier in the same input pass.");

            NowOverlay.Flush();
        }

        Assert.AreEqual(new Vector2(0f, -1f), popupWheel);
    }

    [Test]
    public void ModalPopupContainsRemainingWheelAtItsOwnScrollEdge()
    {
        var popup = new NowRect(20f, 40f, 180f, 120f);
        var parent = new NowRect(0f, 0f, 260f, 220f);
        _pointer.snapshot = new NowInputSnapshot(
            true,
            new Vector2(80f, 80f),
            new Vector2(80f, 80f),
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            new Vector2(0f, -1f),
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            78,
            1f);

        using (NowInput.Begin(_pointer, Surface))
        {
            NowOverlay.BlockAllSurfaces(7002);
            NowOverlay.DeferScreen(popup, 7002, _ => { });

            Assert.AreEqual(Vector2.zero, NowInput.ConsumeScrollDelta(parent));
            NowOverlay.Flush();

            Assert.AreEqual(
                Vector2.zero,
                NowInput.ConsumeScrollDelta(parent),
                "An unhandled wheel tick over a modal popup must be swallowed instead of bubbling to its background.");
        }
    }

    [Test]
    public void RepeatedSameFramePopupPassesKeepOverlayRegistryBounded()
    {
        _pointer.snapshot = new NowInputSnapshot(new Vector2(80f, 80f), false, false, false);
        var popup = new NowRect(20f, 40f, 180f, 120f);

        for (int i = 0; i < 200; ++i)
        {
            using (NowInput.Begin(_pointer, Surface))
            {
                NowOverlay.BlockAllSurfaces(7003);
                NowOverlay.DeferScreen(popup, 7003, _ => { });
            }

            Assert.LessOrEqual(NowOverlay.currentBlockCount, 2);
            Assert.LessOrEqual(NowOverlay.previousBlockCount, 2);
            Assert.AreEqual(1, NowOverlay.registrationOwnerCount);
        }

        NowOverlay.Reset();
        Assert.AreEqual(0, NowOverlay.registrationOwnerCount);
        Assert.AreEqual(0, NowOverlay.currentBlockCount);
        Assert.AreEqual(0, NowOverlay.previousBlockCount);
    }

    [Test]
    public void ProvidersWithoutOverlayFootprintsDoNotAccumulateRegistrationOwners()
    {
        for (int i = 0; i < 200; ++i)
        {
            var provider = new FakePointer
            {
                snapshot = new NowInputSnapshot(new Vector2(80f, 80f), false, false, false)
            };

            using (NowInput.Begin(provider, Surface))
            {
            }
        }

        Assert.AreEqual(
            0,
            NowOverlay.registrationOwnerCount,
            "A provider with no popup footprint has no overlay state that needs to survive its input transaction.");
    }

    [Test]
    public void StaleProviderOwnerExpiresAfterItsFootprintRollsOut()
    {
        var provider = new FakePointer
        {
            snapshot = new NowInputSnapshot(new Vector2(80f, 80f), false, false, false)
        };

        using (NowInput.Begin(provider, Surface))
            NowOverlay.BlockScreen(new NowRect(20f, 40f, 100f, 80f));

        Assert.AreEqual(1, NowOverlay.registrationOwnerCount);

        NowOverlay.ForceNewFrame();
        Assert.AreEqual(
            1,
            NowOverlay.registrationOwnerCount,
            "The immediately previous footprint must stay available for the provider's next pass.");

        NowOverlay.ForceNewFrame();
        Assert.AreEqual(
            0,
            NowOverlay.registrationOwnerCount,
            "Once neither block registry references the provider, its owner state must be released.");
    }

    [Test]
    public void DestroyedRuntimeHostReleasesItsOwnerAndPointerBlocks()
    {
        var hostObject = new GameObject("NowOverlay owner cleanup test");

        try
        {
            using (NowOverlay.Host(hostObject.transform))
            using (NowInput.Begin(_pointer, Surface))
                NowOverlay.BlockScreen(new NowRect(20f, 40f, 100f, 80f));

            Assert.AreEqual(1, NowOverlay.registrationOwnerCount);
            Assert.AreEqual(1, NowOverlay.currentBlockCount);

            Object.DestroyImmediate(hostObject);
            hostObject = null;
            NowOverlay.ForceNewFrame();

            Assert.AreEqual(0, NowOverlay.registrationOwnerCount);
            Assert.AreEqual(0, NowOverlay.currentBlockCount);
            Assert.AreEqual(0, NowOverlay.previousBlockCount);
        }
        finally
        {
            if (hostObject != null)
                Object.DestroyImmediate(hostObject);
        }
    }

    [Test]
    public void FailedSameFramePopupPassRollsBackToTheLastValidRegistration()
    {
        var validPopup = new NowRect(20f, 40f, 80f, 80f);
        var failedPopup = new NowRect(300f, 300f, 80f, 80f);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(40f, 60f), false, false, false);

        using (NowInput.Begin(_pointer, Surface))
            NowOverlay.DeferScreen(validPopup, 7201, _ => { });

        Assert.Throws<System.InvalidOperationException>(() =>
        {
            using (NowInput.Begin(_pointer, Surface))
            {
                NowOverlay.DeferScreen(
                    failedPopup,
                    7202,
                    _ => throw new System.InvalidOperationException("popup replay failed"));
            }
        });

        using (NowInput.Begin(_pointer, Surface))
        {
            Assert.IsTrue(
                NowOverlay.IsPointerBlocked(validPopup.center),
                "The last completed pass remains the authoritative popup footprint.");
            Assert.IsFalse(
                NowOverlay.IsPointerBlocked(failedPopup.center),
                "A throwing deferred pass must not leave its invisible hit block behind.");
        }
    }

    [Test]
    public void OverlayFlushCapRollsBackTheAbandonedPassRegistrations()
    {
        var validPopup = new NowRect(20f, 40f, 80f, 80f);
        var abandonedPopup = new NowRect(300f, 300f, 80f, 80f);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(40f, 60f), false, false, false);

        using (NowInput.Begin(_pointer, Surface))
            NowOverlay.DeferScreen(validPopup, 7251, _ => { });

        using (NowInput.Begin(_pointer, Surface))
        {
            NowOverlay.DrawCallback draw = _ => { };

            for (int i = 0; i <= 1024; ++i)
                NowOverlay.DeferScreen(abandonedPopup, 7300 + i, draw);

            LogAssert.Expect(
                LogType.Error,
                new Regex("NowOverlay\\.Flush aborted after 1024 overlays"));
            NowOverlay.Flush();

            Assert.AreEqual(
                0,
                NowOverlay.currentBlockCount,
                "A capped flush abandons the whole provisional pass, including its hit registrations.");
            Assert.IsTrue(
                NowOverlay.IsPointerBlocked(validPopup.center),
                "The owner's last completed popup footprint remains authoritative.");
            Assert.IsFalse(NowOverlay.IsPointerBlocked(abandonedPopup.center));
        }
    }

    [Test]
    public void NestedOwnerPromotionKeepsOuterRollbackCheckpointAligned()
    {
        var other = new FakePointer
        {
            snapshot = new NowInputSnapshot(new Vector2(40f, 60f), false, false, false)
        };
        var previousOther = new NowRect(20f, 40f, 80f, 80f);
        var outerCurrent = new NowRect(150f, 40f, 80f, 80f);
        var nestedFailed = new NowRect(300f, 300f, 80f, 80f);
        _pointer.snapshot = other.snapshot;

        using (NowInput.Begin(other, Surface))
            NowOverlay.DeferScreen(previousOther, 7301, _ => { });

        NowInput.defaultProvider = _pointer;
        NowInput.BeginScreenFrame(new NowInputSurface(Surface));

        try
        {
            NowOverlay.DeferScreen(outerCurrent, 7302, _ => { });

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                using (NowInput.Begin(other, Surface))
                {
                    NowOverlay.DeferScreen(
                        nestedFailed,
                        7303,
                        _ => throw new System.InvalidOperationException("nested popup replay failed"));
                }
            });

            using (NowInput.Begin(other, Surface))
            {
                Assert.IsTrue(
                    NowOverlay.IsPointerBlocked(previousOther.center),
                    "The nested owner's last valid pass must remain available.");
            }

            Assert.IsFalse(
                NowOverlay.IsPointerBlocked(outerCurrent.center),
                "Rollback must discard the outer block whose callback queue was abandoned.");
            Assert.IsFalse(
                NowOverlay.IsPointerBlocked(nestedFailed.center),
                "Rollback must discard the nested throwing block.");
        }
        finally
        {
            NowInput.EndFrame();
            NowInput.defaultProvider = NowScreenInputProvider.instance;
        }
    }

    [Test]
    public void HostlessIMGUIPopupDoesNotBlockAnotherGUIContext()
    {
        var firstPanel = new NowIMGUIInputProvider();
        var secondPanel = new NowIMGUIInputProvider();
        var popup = new NowRect(20f, 40f, 100f, 80f);

        try
        {
            using (NowInput.Begin(firstPanel, Surface))
            {
                NowOverlay.BlockAllSurfaces(7401);
                NowOverlay.DeferScreen(popup, 7401, _ => { });
            }

            // A later pass promotes the first panel's completed footprint to
            // the previous-pass registry used by base-layer hit tests.
            using (NowInput.Begin(firstPanel, Surface))
            {
                Assert.IsTrue(NowOverlay.IsPointerBlocked(popup.center));
                Assert.IsTrue(NowOverlay.IsPointerInsideOverlay(popup.center));
                Assert.AreEqual(7401, NowOverlay.activeFocusLayerId);
            }

            using (NowInput.Begin(secondPanel, Surface))
            {
                Assert.IsFalse(
                    NowOverlay.IsPointerBlocked(popup.center),
                    "Local coordinates from one EditorWindow are not a modal region in another.");
                Assert.IsFalse(NowOverlay.IsPointerInsideOverlay(popup.center));
                Assert.AreEqual(0, NowOverlay.activeFocusLayerId);
            }
        }
        finally
        {
            firstPanel.ResetState();
            secondPanel.ResetState();
        }
    }

    [Test]
    public void SameFrameDropdownWheelScrollsPopupWithoutMovingParent()
    {
        const int outerId = 8101;
        const int dropdownId = 8102;
        var outerRect = new NowRect(0f, 0f, 260f, 180f);
        var fieldRect = new NowRect(20f, 20f, 180f, 30f);
        var options = new List<string>(40);
        int selected = 0;
        int pass = 0;

        for (int i = 0; i < 40; ++i)
            options.Add($"Option {i + 1}");

        ref bool open = ref NowControlState.Get<bool>(dropdownId);
        open = true;

        void Draw(Vector2 wheel)
        {
            var snapshot = new NowInputSnapshot(
                true,
                new Vector2(80f, 90f),
                new Vector2(80f, 90f),
                Vector2.zero,
                NowPointerButtons.None,
                NowPointerButtons.None,
                NowPointerButtons.None,
                wheel,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                500,
                5f)
            {
                inputPass = ++pass
            };
            _pointer.snapshot = snapshot;

            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                using (Now.ScrollView(outerRect, NowId.Resolved(outerId)).Begin())
                {
                    Now.Dropdown(
                            fieldRect,
                            NowId.Resolved(dropdownId),
                            options)
                        .Draw(ref selected);
                    NowLayout.ReserveRect(height: 500f, stretchWidth: true);
                }

                NowOverlay.Flush();
            }
        }

        // Warm both one-pass scroll layouts while Unity's frame and the test
        // snapshot frame remain fixed. This is the Editor IMGUI failure mode:
        // Layout, input and Repaint are separate passes, not separate frames.
        Draw(Vector2.zero);
        Draw(Vector2.zero);
        Draw(Vector2.zero);

        ref Vector2 outerScroll = ref NowControlState.Get<Vector2>(outerId);
        int popupScrollId = NowInput.GetId(dropdownId, "popup-scroll");
        ref Vector2 popupScroll = ref NowControlState.Get<Vector2>(popupScrollId);

        Assert.AreEqual(Vector2.zero, outerScroll);
        Assert.AreEqual(Vector2.zero, popupScroll);

        Draw(new Vector2(0f, -2f));

        Assert.Greater(
            popupScroll.y,
            0f,
            "Wheel input over the dropdown's own scrolling popup must move that popup.");
        Assert.AreEqual(
            0f,
            outerScroll.y,
            0.001f,
            "The enclosing scroll view must not process the popup's wheel tick.");

        popupScroll.y = 100000f;
        Draw(Vector2.zero);
        float popupBottom = popupScroll.y;
        float outerBeforeEdgeWheel = outerScroll.y;

        Draw(new Vector2(0f, -2f));

        Assert.AreEqual(
            popupBottom,
            popupScroll.y,
            0.001f,
            "The popup fixture must remain pinned at its lower scroll edge.");
        Assert.AreEqual(
            outerBeforeEdgeWheel,
            outerScroll.y,
            0.001f,
            "A modal popup must contain the wheel tick even when it cannot scroll farther.");
    }

    [Test]
    public void NativeIMGUIDropdownWheelIsConsumedByPopupAndRepaintsOwningProvider()
    {
        const int HostControlId = 8111;
        const int OuterId = 8112;
        const int DropdownId = 8113;
        var provider = new NowIMGUIInputProvider(HostControlId, new object());
        var inputSurface = new NowInputSurface(Surface);
        var outerRect = new NowRect(0f, 0f, 260f, 180f);
        var fieldRect = new NowRect(20f, 20f, 180f, 30f);
        var options = new List<string>(40);
        var snapshotField = typeof(NowInput).GetField(
            "_snapshot",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static);
        Event previousEvent = Event.current;
        bool previousChanged = GUI.changed;
        System.Action previousRepaint = NowIMGUIInputProvider.repaintRequested;
        System.Action<NowIMGUIInputProvider> previousHostRepaint =
            NowIMGUIInputProvider.hostRepaintRequested;
        int selected = 0;
        int hostRepaintCount = 0;
        NowIMGUIInputProvider repaintOwner = null;

        for (int i = 0; i < 40; ++i)
            options.Add($"Option {i + 1}");

        Assert.NotNull(snapshotField);
        NowControlState.Get<bool>(DropdownId) = true;

        void Draw(Event inputEvent, EventType routedType)
        {
            Event.current = null;

            using (NowInput.Begin(provider, inputSurface))
            {
                Assert.IsTrue(provider.TryGetSnapshot(
                    inputSurface,
                    inputEvent,
                    routedType,
                    ownsCapture: false,
                    out var snapshot));
                snapshotField.SetValue(null, snapshot);

                using (_drawList.Begin(Surface))
                {
                    using (Now.ScrollView(
                               outerRect,
                               NowId.Resolved(OuterId)).Begin())
                    {
                        Now.Dropdown(
                                fieldRect,
                                NowId.Resolved(DropdownId),
                                options)
                            .Draw(ref selected);
                        NowLayout.ReserveRect(height: 500f, stretchWidth: true);
                    }

                    NowOverlay.Flush();
                }
            }
        }

        try
        {
            GUI.changed = false;
            NowIMGUIInputProvider.repaintRequested = null;
            NowIMGUIInputProvider.hostRepaintRequested = owner =>
            {
                ++hostRepaintCount;
                repaintOwner = owner;
            };

            for (int i = 0; i < 3; ++i)
            {
                Draw(
                    new Event
                    {
                        type = EventType.Layout,
                        mousePosition = new Vector2(80f, 90f)
                    },
                    EventType.Layout);
            }

            ref Vector2 outerScroll =
                ref NowControlState.Get<Vector2>(OuterId);
            int popupScrollId = NowInput.GetId(
                DropdownId,
                "popup-scroll");
            ref Vector2 popupScroll =
                ref NowControlState.Get<Vector2>(popupScrollId);
            Assert.AreEqual(Vector2.zero, outerScroll);
            Assert.AreEqual(Vector2.zero, popupScroll);
            hostRepaintCount = 0;
            repaintOwner = null;

            var wheel = new Event
            {
                type = EventType.ScrollWheel,
                mousePosition = new Vector2(80f, 90f),
                delta = new Vector2(0f, 6f)
            };
            Draw(wheel, EventType.ScrollWheel);

            Assert.Greater(
                popupScroll.y,
                0f,
                "The native wheel tick must move the dropdown's scrolling popup.");
            Assert.AreEqual(
                0f,
                outerScroll.y,
                0.001f,
                "The enclosing NowUI scroll view must not receive the popup's native wheel tick.");
            Assert.AreEqual(
                EventType.Used,
                wheel.type,
                "The consumed popup wheel must not bubble into a native parent scroll view.");
            Assert.AreEqual(1, hostRepaintCount);
            Assert.AreSame(
                provider,
                repaintOwner,
                "Wheel handling must repaint the Editor GUI provider that sampled the native event.");
        }
        finally
        {
            provider.ResetState(releaseNativeCapture: false);
            NowIMGUIInputProvider.repaintRequested = previousRepaint;
            NowIMGUIInputProvider.hostRepaintRequested =
                previousHostRepaint;
            GUI.changed = previousChanged;
            Event.current = previousEvent;
        }
    }

    [Test]
    public void StableOpenDropdownDoesNotRequestAnotherImmediateRepaint()
    {
        int selected = 0;
        int dropdownId = ResolveControlId("stable-dd");
        NowControlState.Get<bool>(dropdownId) = true;
        _pointer.snapshot = new NowInputSnapshot(new Vector2(500f, 500f), false, false, false);
        NowControlState.BeginRepaintTracking();
        bool repaint;

        try
        {
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                Now.Dropdown(FieldRect, "stable-dd", Options).Draw(ref selected);
                NowOverlay.Flush();
            }
        }
        finally
        {
            repaint = NowControlState.EndRepaintTracking();
        }

        Assert.IsFalse(
            repaint,
            "A static open popup must converge to idle instead of rebuilding an editor RenderTexture at 60 Hz.");
    }

    [Test]
    public void DropdownArrowsMoveHighlightFromSelectionAndSubmitCommits()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;
        var down = new Vector2(0f, -1f);

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, released: true));

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, navigation: down));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsFalse(NowControlState.Get<bool>(ResolveControlId("dd")), "Submit must close the popup.");
        Assert.IsTrue(
            DrawDropdownFrame(ref selected, Snapshot(fieldCenter)),
            "The committed highlight must apply on the next Draw.");
        Assert.AreEqual(1, selected, "One down pulse from the selected item must highlight the next option.");
    }

    [Test]
    public void DropdownTypeToSelectJumpsToTheMatchingOption()
    {
        int selected = 0;
        var fieldCenter = FieldRect.center;

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, down: true, pressed: true));
        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, released: true));

        NowOverlay.ForceNewFrame();
        _pointer.snapshot = Snapshot(fieldCenter);
        _keyboard.frame = new NowTextInputFrame { characters = "h" };
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            Now.Dropdown(FieldRect, "dd", Options).Draw(ref selected);
            NowOverlay.Flush();
        }

        DrawDropdownFrame(ref selected, Snapshot(fieldCenter, submitPressed: true));

        Assert.IsTrue(DrawDropdownFrame(ref selected, Snapshot(fieldCenter)));
        Assert.AreEqual(2, selected, "Typing 'h' must highlight High and submit must commit it.");
    }

    [Test]
    public void DropdownAcceptsSubmitInALaterInputPassOfTheSameFrame()
    {
        int selected = 0;
        var center = FieldRect.center;

        var press = Snapshot(center, down: true, pressed: true);
        press.frame = 700;
        press.inputPass = 1;
        DrawDropdownFrame(ref selected, press);

        var release = Snapshot(center, released: true);
        release.frame = 700;
        release.inputPass = 2;
        DrawDropdownFrame(ref selected, release);

        var navigation = Snapshot(center, navigation: new Vector2(0f, -1f));
        navigation.frame = 700;
        navigation.inputPass = 3;
        DrawDropdownFrame(ref selected, navigation);

        var neutral = Snapshot(center);
        neutral.frame = 700;
        neutral.inputPass = 4;
        DrawDropdownFrame(ref selected, neutral);

        var submit = Snapshot(center, submitPressed: true);
        submit.frame = 700;
        submit.inputPass = 5;
        DrawDropdownFrame(ref selected, submit);

        var commit = Snapshot(center);
        commit.frame = 700;
        commit.inputPass = 6;

        Assert.IsTrue(DrawDropdownFrame(ref selected, commit));
        Assert.AreEqual(1, selected);
    }

    sealed class NestedDropdownView : INowView
    {
        public int selected;

        public void Draw(NowViewContext context)
        {
            Now.Dropdown(
                    new NowRect(context.rect.x + 10f, context.rect.y + 10f, 140f, 28f),
                    "nested-dd",
                    Options)
                .Draw(ref selected);
        }
    }

    void DrawStackFrame(NowViewStack stack, NowInputSnapshot snapshot)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            stack.Draw(new NowRect(0f, 0f, Surface.x, Surface.y));
            NowOverlay.Flush();
        }
    }

    [Test]
    public void CancelClosesTheNestedDropdownBeforeThePopupView()
    {
        var stack = new NowViewStack();
        var idle = new Vector2(500f, 500f);
        int dropdownId = ResolveControlId("nested-dd");

        stack.Push(new NestedDropdownView(), NowViewOptions.Popup(ViewPopupRect, NowViewTransitionPreset.None, 0f));
        NowControlState.Get<bool>(dropdownId) = true;

        DrawStackFrame(stack, Snapshot(idle));

        DrawStackFrame(stack, Snapshot(idle, cancelPressed: true));
        Assert.AreEqual(1, stack.count, "Cancel must close only the nested dropdown, not the popup view.");
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId), "Cancel must close the nested dropdown.");

        DrawStackFrame(stack, Snapshot(idle));
        DrawStackFrame(stack, Snapshot(idle, cancelPressed: true));
        Assert.AreEqual(0, stack.count, "With no nested overlay left, cancel must close the popup view.");
    }

    [Test]
    public void OutsidePressClosesTheNestedDropdownBeforeThePopupView()
    {
        var stack = new NowViewStack();
        var outside = new Vector2(500f, 500f);
        int dropdownId = ResolveControlId("nested-dd");

        stack.Push(new NestedDropdownView(), NowViewOptions.Popup(ViewPopupRect, NowViewTransitionPreset.None, 0f));
        NowControlState.Get<bool>(dropdownId) = true;

        DrawStackFrame(stack, Snapshot(outside));

        DrawStackFrame(stack, Snapshot(outside, down: true, pressed: true));
        Assert.AreEqual(1, stack.count, "An outside press must dismiss only the nested dropdown.");
        Assert.IsFalse(NowControlState.Get<bool>(dropdownId));

        DrawStackFrame(stack, Snapshot(outside, released: true));
        DrawStackFrame(stack, Snapshot(outside, down: true, pressed: true));
        Assert.AreEqual(0, stack.count, "The next outside press must close the popup view.");
    }

    sealed class KeyBindingView : INowView
    {
        public Key value = Key.E;

        public void Draw(NowViewContext context)
        {
            Now.KeyBindingField(new NowRect(context.rect.x + 10f, context.rect.y + 10f, 140f, 30f), "bind")
                .Draw(ref value);
        }
    }

    void DrawBindingFrame(NowViewStack stack, NowInputSnapshot snapshot, Key pressed = Key.None)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = snapshot;
        _keys.frame = new NowKeyInputFrame { pressedKey = pressed };
        NowKeyInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            stack.Draw(new NowRect(0f, 0f, Surface.x, Surface.y));
            NowOverlay.Flush();
        }
    }

    [Test]
    public void EscapeCancellingKeyCaptureDoesNotCloseTheEnclosingPopup()
    {
        var stack = new NowViewStack();
        var view = new KeyBindingView();
        var insideField = new Vector2(ViewPopupRect.x + 20f, ViewPopupRect.y + 20f);

        stack.Push(view, NowViewOptions.Popup(ViewPopupRect, NowViewTransitionPreset.None, 0f));

        DrawBindingFrame(stack, Snapshot(insideField, down: true, pressed: true));
        DrawBindingFrame(stack, Snapshot(insideField, released: true));

        DrawBindingFrame(stack, Snapshot(insideField, cancelPressed: true), Key.Escape);

        Assert.AreEqual(1, stack.count, "Escape that cancels a key capture must not also close the popup.");
        Assert.AreEqual(Key.E, view.value, "Escape must cancel the capture without rebinding.");

        DrawBindingFrame(stack, Snapshot(insideField));
        DrawBindingFrame(stack, Snapshot(insideField, cancelPressed: true));

        Assert.AreEqual(0, stack.count, "With no capture in progress, cancel must close the popup.");
    }

    [Test]
    public void KeyBindingAcceptsLaterKeyDownWhenUnityFrameHasNotAdvanced()
    {
        Key value = Key.E;
        var rect = new NowRect(20f, 20f, 160f, 30f);
        var center = rect.center;

        bool Draw(NowInputSnapshot snapshot, Key key = Key.None)
        {
            _pointer.snapshot = snapshot;
            _keys.frame = new NowKeyInputFrame { pressedKey = key };

            if (key != Key.None)
                NowKeyInput.Invalidate();

            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
                return Now.KeyBindingField(rect, "same-frame-bind").Draw(ref value);
        }

        var press = Snapshot(center, down: true, pressed: true);
        press.frame = 500;
        press.inputPass = 1;
        Assert.IsFalse(Draw(press));

        var release = Snapshot(center, released: true);
        release.frame = 500;
        release.inputPass = 2;
        Assert.IsFalse(Draw(release));

        var keyDown = Snapshot(center);
        keyDown.frame = 500;
        keyDown.inputPass = 3;

        Assert.IsTrue(
            Draw(keyDown, Key.Q),
            "A native key event in a later IMGUI pass must not be rejected merely because Time.frameCount is unchanged.");
        Assert.AreEqual(Key.Q, value);
    }
}
