using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NowUI;

/// <summary>
/// TextField, overlay/occlusion, ScrollView and Dropdown tests — all driven
/// through fake pointer and keyboard sources.
/// </summary>
public class NowControlsAdvancedTests
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

    sealed class BufferedKeyboard : INowTextInputSource, INowTextInputBuffer
    {
        public string pending;

        public int discardCount;

        public bool TryGetFrame(out NowTextInputFrame result)
        {
            result = new NowTextInputFrame { characters = pending };
            pending = null;
            return true;
        }

        public void DiscardPendingText()
        {
            ++discardCount;
            pending = null;
        }
    }

    sealed class RecordingRenderer : NowControlRenderer
    {
        public int buttons;
        public int checkboxes;
        public int radios;
        public int sliders;
        public int textInputFrames;
        public int dropdownFields;
        public int popupBackgrounds;
        public int popupItems;
        public int contextMenuItems;
        public int scrollbars;
        public int verticalScrollbars;
        public int horizontalScrollbars;

        public override void DrawButton(in NowButtonRenderContext context)
        {
            ++buttons;
            base.DrawButton(context);
        }

        public override void DrawCheckbox(in NowToggleRenderContext context)
        {
            ++checkboxes;
            base.DrawCheckbox(context);
        }

        public override void DrawRadio(in NowToggleRenderContext context)
        {
            ++radios;
            base.DrawRadio(context);
        }

        public override void DrawSlider(in NowSliderRenderContext context)
        {
            ++sliders;
            base.DrawSlider(context);
        }

        public override void DrawTextInputFrame(in NowControlFrameRenderContext context)
        {
            ++textInputFrames;
            base.DrawTextInputFrame(context);
        }

        public override void DrawDropdownField(in NowDropdownFieldRenderContext context)
        {
            ++dropdownFields;
            base.DrawDropdownField(context);
        }

        public override void DrawPopupBackground(NowThemeAsset themeAsset, NowRect rect, bool menu)
        {
            ++popupBackgrounds;
            base.DrawPopupBackground(themeAsset, rect, menu);
        }

        public override void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            ++popupItems;
            base.DrawPopupItem(context);
        }

        public override void DrawContextMenuItem(in NowPopupItemRenderContext context)
        {
            ++contextMenuItems;
            base.DrawContextMenuItem(context);
        }

        public override void DrawScrollbar(in NowScrollbarRenderContext context)
        {
            ++scrollbars;

            if (context.axis == NowScrollbarAxis.Vertical)
                ++verticalScrollbars;
            else
                ++horizontalScrollbars;

            base.DrawScrollbar(context);
        }
    }

    static void SetRenderer(NowThemeAsset theme, NowControlRenderer renderer)
    {
        typeof(NowThemeAsset)
            .GetField("_controlRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(theme, renderer);
    }

    static readonly Vector2 Surface = new Vector2(512, 256);
    static readonly NowRect FieldRect = new NowRect(20, 20, 200, 30);

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowTextInput.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        NowTextInput.source = _keyboard;
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
    }

    bool DrawTextFieldFrame(ref string text, NowTextInputFrame keys)
    {
        _keyboard.frame = keys;
        NowTextInput.Invalidate();
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.TextField(FieldRect, "name").Draw(ref text);

        return changed;
    }

    void DrawNestedScrollViewsFrame()
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 240, 140), "outer").Begin())
        {
            NowLayout.Rect(height: 10f, stretchWidth: true);

            using (NowLayout.ScrollView("inner").SetHeight(70f).Begin())
            {
                for (int i = 0; i < 8; ++i)
                    NowLayout.Rect(height: 30f, stretchWidth: true);
            }

            NowLayout.Rect(height: 300f, stretchWidth: true);
        }
    }

    void FocusField()
    {
        NowFocus.Focus(NowControls.GetControlId("name"));
    }

    [Test]
    public void TextFieldTypesCharactersWhileFocused()
    {
        string text = string.Empty;
        FocusField();

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "Hi" }));
        Assert.AreEqual("Hi", text);

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "!" }));
        Assert.AreEqual("Hi!", text);
    }

    [Test]
    public void BuiltInControlsRouteVisualsThroughThemeRenderer()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        var previousFont = Now.defaultFont;
        var font = ScriptableObject.CreateInstance<NowFont>();

        try
        {
            SetRenderer(theme, renderer);
            Now.defaultFont = font;
            _pointer.snapshot = new NowInputSnapshot(new Vector2(400, 200), false, false, false);

            bool toggle = true;
            float slider = 0.5f;
            string field = "name";
            string area = "line";
            int selected = 0;
            var options = new List<string> { "Low", "Medium", "High" };

            using (NowInput.Begin(_pointer, Surface))
            {
                int dropdownId = NowControls.GetControlId("quality");
                NowControlState.Get<bool>(dropdownId) = true;
            }

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                Now.Button(new NowRect(0, 0, 100, 28), "Save").Draw();
                Now.Checkbox(new NowRect(0, 32, 140, 28), "Enabled").Draw(ref toggle);
                Now.Radio(new NowRect(0, 64, 140, 28), "High", true).Draw();
                Now.Slider(new NowRect(0, 96, 160, 20), 0f, 1f).Draw(ref slider);
                Now.TextField(new NowRect(0, 124, 160, 30), "field").Draw(ref field);
                Now.TextArea(new NowRect(170, 0, 160, 60), "area").Draw(ref area);
                Now.Dropdown(new NowRect(170, 66, 160, 30), "quality", options).Draw(ref selected);

                var metrics = NowScrollbar.Calculate(
                    NowScrollbarAxis.Vertical,
                    new NowRect(340, 0, 8, 80),
                    40f,
                    120f,
                    0f,
                    theme.controlStyles.scrollbarMinThumbSize);
                NowScrollbar.Draw(theme, 8181, NowScrollbarAxis.Vertical, metrics);

                NowContextMenu.Open(7001, new Vector2(16, 160));
                if (NowContextMenu.Begin(7001))
                {
                    NowContextMenu.Item("Copy");
                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }

            Assert.GreaterOrEqual(renderer.buttons, 1);
            Assert.GreaterOrEqual(renderer.checkboxes, 1);
            Assert.GreaterOrEqual(renderer.radios, 1);
            Assert.GreaterOrEqual(renderer.sliders, 1);
            Assert.GreaterOrEqual(renderer.textInputFrames, 2);
            Assert.GreaterOrEqual(renderer.dropdownFields, 1);
            Assert.GreaterOrEqual(renderer.popupBackgrounds, 1);
            Assert.GreaterOrEqual(renderer.popupItems, 1);
            Assert.GreaterOrEqual(renderer.contextMenuItems, 1);
            Assert.GreaterOrEqual(renderer.scrollbars, 1);
        }
        finally
        {
            Now.defaultFont = previousFont;
            Object.DestroyImmediate(font);
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void TextFieldIgnoresTypingWhenUnfocused()
    {
        string text = "keep";

        Assert.IsFalse(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "x" }));
        Assert.AreEqual("keep", text);
    }

    [Test]
    public void TextFieldDiscardsBufferedCharactersWhenFocusedByClick()
    {
        var keyboard = new BufferedKeyboard { pending = "ghost" };
        NowTextInput.source = keyboard;
        string text = string.Empty;

        _pointer.snapshot = new NowInputSnapshot(new Vector2(FieldRect.x + 10f, FieldRect.y + 10f), true, true, false);
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Assert.IsFalse(Now.TextField(FieldRect, "name").Draw(ref text));

        Assert.AreEqual(string.Empty, text);
        Assert.AreEqual(1, keyboard.discardCount);

        _pointer.snapshot = default;
        keyboard.pending = "n";
        NowTextInput.Invalidate();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Assert.IsTrue(Now.TextField(FieldRect, "name").Draw(ref text));

        Assert.AreEqual("n", text);
    }

    [Test]
    public void TextFieldBackspaceDeletes()
    {
        string text = "abc";
        FocusField();

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { backspaceHeld = true }));
        Assert.AreEqual("ab", text);
    }

    [Test]
    public void TextFieldSelectAllThenTypeReplaces()
    {
        string text = "old text";
        FocusField();

        DrawTextFieldFrame(ref text, new NowTextInputFrame { selectAllPressed = true, command = true });
        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "n" }));
        Assert.AreEqual("n", text);
    }

    [Test]
    public void TextFieldCopyPasteRoundTrip()
    {
        var previousSet = NowClipboard.setText;
        var previousGet = NowClipboard.getText;
        string clipboard = string.Empty;
        NowClipboard.setText = value => clipboard = value;
        NowClipboard.getText = () => clipboard;

        try
        {
            string text = "copyme";
            FocusField();

            DrawTextFieldFrame(ref text, new NowTextInputFrame { selectAllPressed = true, command = true });
            DrawTextFieldFrame(ref text, new NowTextInputFrame { copyPressed = true, command = true });
            Assert.AreEqual("copyme", clipboard);

            DrawTextFieldFrame(ref text, new NowTextInputFrame { pastePressed = true, command = true });
            Assert.AreEqual("copyme", text, "Paste over a full selection keeps the same text.");

            DrawTextFieldFrame(ref text, new NowTextInputFrame { pastePressed = true, command = true });
            Assert.AreEqual("copymecopyme", text, "Second paste appends at the caret.");
        }
        finally
        {
            NowClipboard.setText = previousSet;
            NowClipboard.getText = previousGet;
        }
    }

    [Test]
    public void TextFieldClickPlacesCaretAndEscapeBlurs()
    {
        string text = "hello";
        FocusField();

        _pointer.snapshot = new NowInputSnapshot(new Vector2(FieldRect.xMax - 4, 35), true, true, false);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.TextField(FieldRect, "name").Draw(ref text);

        _pointer.snapshot = default;
        DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "!" });
        Assert.AreEqual("hello!", text);

        DrawTextFieldFrame(ref text, new NowTextInputFrame { escapePressed = true });
        Assert.AreEqual(0, NowFocus.focusedId, "Escape must blur the field.");
    }

    [Test]
    public void TextFieldImeCompositionSuppressesKeysAndKeepsText()
    {
        string text = "ab";
        FocusField();

        Assert.IsFalse(DrawTextFieldFrame(ref text, new NowTextInputFrame
        {
            composition = "か",
            backspaceHeld = true,
            enterPressed = true
        }));

        Assert.AreEqual("ab", text, "Composition must not edit the text.");
        Assert.AreNotEqual(0, NowFocus.focusedId, "Enter belongs to the IME while composing.");

        Assert.IsTrue(DrawTextFieldFrame(ref text, new NowTextInputFrame { characters = "か" }));
        Assert.AreEqual("abか", text);
    }

    [Test]
    public void RaycastPressGateLatchesAtPressTime()
    {
        bool latch = true;

        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: false));

        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: false));
        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: true),
            "A press that began blocked stays blocked for the whole press even if the gate would now allow (no click-through).");
        Assert.IsFalse(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: true));

        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: true));

        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: false, allowedNow: true));
        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: false));
        Assert.IsTrue(NowRaycastGate.UpdatePressGate(ref latch, buttonsWereDown: true, allowedNow: false));
    }

    [Test]
    public void OverlayDeferredDrawRunsAtFlush()
    {
        bool ran = false;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowOverlay.Defer(new NowRect(0, 0, 100, 100), () =>
            {
                ran = true;
                Now.Rectangle(new NowRect(10, 10, 50, 50)).SetColor(Color.red).Draw();
            });

            Assert.IsFalse(ran, "Deferred draws must not run inline.");
        }

        Assert.IsTrue(ran, "Deferred draws must run when the capture flushes.");
        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void OverlayBlocksPointerUnderneathNextFrame()
    {
        var blocked = new NowRect(0, 0, 100, 100);

        using (NowInput.Begin(_pointer, Surface))
        {
            NowOverlay.Block(blocked);
            Assert.IsFalse(NowOverlay.IsPointerBlocked(new Vector2(50, 50)), "Blocking applies one frame late.");
        }

        NowOverlay.ForceNewFrame();

        Assert.IsTrue(NowOverlay.IsPointerBlocked(new Vector2(50, 50)));
        Assert.IsFalse(NowOverlay.IsPointerBlocked(new Vector2(200, 200)));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(50, 50), false, false, false);

        using (NowInput.Begin(_pointer, Surface))
        {
            var interaction = NowInput.Interact(99, new NowRect(0, 0, 100, 100));
            Assert.IsFalse(interaction.hovered);
        }
    }

    [Test]
    public void ScrollViewClampsAndStoresScroll()
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "list").Begin())
        {
            for (int i = 0; i < 10; ++i)
                NowLayout.Rect(new NowLayoutOptions().SetSize(180, 30));
        }

        _pointer.snapshot = new NowInputSnapshot(
            true, new Vector2(100, 50), new Vector2(100, 50), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            new Vector2(0f, -2f), Vector2.zero,
            false, false, false, false, false, false, 2, 2f);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "list").Begin())
        {
            for (int i = 0; i < 10; ++i)
                NowLayout.Rect(new NowLayoutOptions().SetSize(180, 30));
        }

        int scrollId;

        using (NowInput.Begin(_pointer, Surface))
            scrollId = NowControls.GetControlId("list");

        ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
        Assert.Greater(scroll.y, 0f, "Wheel must scroll the content.");
        Assert.LessOrEqual(scroll.y, 200f, "Scroll must clamp to content - viewport.");
    }

    [Test]
    public void ScrollViewDrawsHorizontalScrollbarForWideContent()
    {
        NowLayout.Reset();

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();

        try
        {
            SetRenderer(theme, renderer);

            void DrawFrame()
            {
                using (NowTheme.Scope(theme))
                using (NowInput.Begin(_pointer, Surface))
                using (_drawList.Begin(Surface))
                using (Now.ScrollView(new NowRect(0, 0, 200, 100), "wide").Begin())
                {
                    NowLayout.Rect(new NowLayoutOptions().SetSize(260f, 40f));
                }
            }

            DrawFrame();
            int horizontalBefore = renderer.horizontalScrollbars;
            int verticalBefore = renderer.verticalScrollbars;

            DrawFrame();

            Assert.Greater(renderer.horizontalScrollbars, horizontalBefore, "Wide cached content should draw a horizontal scrollbar.");
            Assert.AreEqual(verticalBefore, renderer.verticalScrollbars, "Short content should not draw a vertical scrollbar.");
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
            NowLayout.Reset();
        }
    }

    [Test]
    public void ScrollViewVerticalScrollbarCanForceHorizontalScrollbar()
    {
        NowLayout.Reset();

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();

        try
        {
            SetRenderer(theme, renderer);

            void DrawFrame()
            {
                using (NowTheme.Scope(theme))
                using (NowInput.Begin(_pointer, Surface))
                using (_drawList.Begin(Surface))
                using (Now.ScrollView(new NowRect(0, 0, 200, 100), "coupled").Begin())
                {
                    NowLayout.Rect(new NowLayoutOptions().SetSize(195f, 130f));
                }
            }

            DrawFrame();
            int horizontalBefore = renderer.horizontalScrollbars;
            int verticalBefore = renderer.verticalScrollbars;

            DrawFrame();

            Assert.Greater(renderer.verticalScrollbars, verticalBefore, "Tall cached content should draw a vertical scrollbar.");
            Assert.Greater(renderer.horizontalScrollbars, horizontalBefore, "The vertical scrollbar gutter should reduce width enough to reveal horizontal overflow.");
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
            NowLayout.Reset();
        }
    }

    [Test]
    public void ScrollViewHorizontalWheelUpdatesScrollPosition()
    {
        NowLayout.Reset();

        try
        {
            void DrawFrame()
            {
                using (NowInput.Begin(_pointer, Surface))
                using (_drawList.Begin(Surface))
                using (Now.ScrollView(new NowRect(0, 0, 200, 100), "wheel-wide").Begin())
                {
                    NowLayout.Rect(new NowLayoutOptions().SetSize(260f, 40f));
                }
            }

            DrawFrame();
            DrawFrame();

            _pointer.snapshot = new NowInputSnapshot(
                true, new Vector2(100f, 50f), new Vector2(100f, 50f), Vector2.zero,
                NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
                new Vector2(2f, 0f), Vector2.zero,
                false, false, false, false, false, false, 2, 2f);

            DrawFrame();

            int scrollId;

            using (NowInput.Begin(_pointer, Surface))
                scrollId = NowControls.GetControlId("wheel-wide");

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
            Assert.Greater(scroll.x, 0f, "Horizontal wheel delta should scroll wide content.");
            Assert.LessOrEqual(scroll.x, 60f, "Horizontal scroll must clamp to content - viewport.");
        }
        finally
        {
            NowLayout.Reset();
        }
    }

    [Test]
    public void ScrollViewWheelAtEdgeFallsThroughToParent()
    {
        DrawNestedScrollViewsFrame();
        DrawNestedScrollViewsFrame();

        int outerId;
        int innerId;

        using (NowInput.Begin(_pointer, Surface))
        {
            outerId = NowControls.GetControlId("outer");
            innerId = NowControls.GetControlId("inner");
        }

        ref Vector2 outerScroll = ref NowControlState.Get<Vector2>(outerId);
        ref Vector2 innerScroll = ref NowControlState.Get<Vector2>(innerId);
        outerScroll.y = 20f;
        innerScroll.y = 0f;

        _pointer.snapshot = new NowInputSnapshot(
            true, new Vector2(100f, 45f), new Vector2(100f, 45f), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            new Vector2(0f, 2f), Vector2.zero,
            false, false, false, false, false, false, 2, 2f);

        DrawNestedScrollViewsFrame();

        Assert.AreEqual(0f, innerScroll.y, 0.001f, "Fixture must keep the inner scroll view pinned at its top.");
        Assert.Less(outerScroll.y, 20f, "Wheel-up at an inner scroll edge should remain available to the parent scroll view.");
    }

    [Test]
    public void ScrollViewScrollsFocusedCulledControlIntoView()
    {
        const int scrollId = 500;
        var viewport = new NowRect(0, 0, 200, 60);

        void DrawFrame()
        {
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            using (Now.ScrollView(viewport, scrollId).Begin())
            {
                NowLayout.Button("One").SetId(1).SetHeight(30).Draw();
                NowLayout.Button("Two").SetId(2).SetHeight(30).Draw();
                NowLayout.Button("Three").SetId(3).SetHeight(30).Draw();
                NowLayout.Button("Four").SetId(4).SetHeight(30).Draw();
            }
        }

        _pointer.snapshot = default;
        DrawFrame();

        NowFocus.Focus(3);
        DrawFrame();

        ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
        Assert.Greater(scroll.y, 0f, "Focusing a culled child should scroll the viewport toward it.");
    }

    [Test]
    public void ScrollViewWheelCanMoveFocusedControlOutOfView()
    {
        NowLayout.Reset();

        const int scrollId = 501;
        var viewport = new NowRect(0, 0, 200, 60);

        try
        {
            void DrawFrame(NowInputSnapshot snapshot = default)
            {
                _pointer.snapshot = snapshot;

                using (NowInput.Begin(_pointer, Surface))
                using (_drawList.Begin(Surface))
                using (Now.ScrollView(viewport, scrollId).Begin())
                {
                    NowLayout.Button("One").SetId(1).SetHeight(30).Draw();
                    NowLayout.Button("Two").SetId(2).SetHeight(30).Draw();
                    NowLayout.Button("Three").SetId(3).SetHeight(30).Draw();
                    NowLayout.Button("Four").SetId(4).SetHeight(30).Draw();
                }
            }

            DrawFrame();
            DrawFrame();

            NowFocus.Focus(1);
            DrawFrame();

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
            Assert.AreEqual(0f, scroll.y, 0.001f);

            DrawFrame(new NowInputSnapshot(
                true, new Vector2(100f, 30f), new Vector2(100f, 30f), Vector2.zero,
                NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
                new Vector2(0f, -2f), Vector2.zero,
                false, false, false, false, false, false, 2, 2f));

            float scrolled = scroll.y;
            Assert.Greater(scrolled, 0f, "Wheel input should be able to move away from the focused child.");

            DrawFrame();

            Assert.AreEqual(scrolled, scroll.y, 0.001f, "The next frame should not snap back to the focused child.");
        }
        finally
        {
            NowLayout.Reset();
        }
    }

    [Test]
    public void DropdownOpensAndAppliesPendingSelection()
    {
        var options = new List<string> { "Low", "Medium", "High" };
        int selected = 0;
        var rect = new NowRect(20, 20, 160, 30);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(60, 35), true, true, false);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.Dropdown(rect, "quality", options).Draw(ref selected);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(60, 35), false, false, true);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.Dropdown(rect, "quality", options).Draw(ref selected);

        int dropdownId;

        using (NowInput.Begin(_pointer, Surface))
            dropdownId = NowControls.GetControlId("quality");

        Assert.IsTrue(NowControlState.Get<bool>(dropdownId), "Click must open the dropdown.");

        NowControlState.Get<int>(NowInput.GetId(dropdownId, "pending")) = 3;

        _pointer.snapshot = default;
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.Dropdown(rect, "quality", options).Draw(ref selected);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, selected, "Pending selection 3 maps to index 2.");
    }
}
