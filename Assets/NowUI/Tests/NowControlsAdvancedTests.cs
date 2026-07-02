using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
        public NowRect lastPopupRect;
        public NowRect lastMenuPopupRect;

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
            lastPopupRect = rect;

            if (menu)
                lastMenuPopupRect = rect;

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
        NowContextMenu.Reset();
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
        NowContextMenu.Reset();
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

    ref NowTextEditState FieldState()
    {
        return ref NowControlState.Get<NowTextEditState>(NowControls.GetControlId("name"));
    }

    static Vector2 TextFieldPoint(string textBefore)
    {
        var theme = NowTheme.themeAsset;
        var textStyle = theme.Text(default, NowTextStyle.Body);
        float lineHeight = textStyle.font != null ? textStyle.font.GetLineHeight(textStyle.fontStyle) * textStyle.fontSize : textStyle.fontSize * 1.2f;
        var inner = theme.controlRenderer.TextFieldInnerRect(theme, FieldRect, lineHeight);
        float x = inner.x + (textStyle.font != null ? textStyle.font.MeasureText(textBefore, textStyle.fontSize, textStyle.fontStyle).x : 0f) + 1f;
        return new Vector2(x, inner.y + inner.height * 0.5f);
    }

    void PointerFrame(ref string text, Vector2 point, bool down, bool pressed, bool released)
    {
        _pointer.snapshot = new NowInputSnapshot(point, down, pressed, released);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            Now.TextField(FieldRect, "name").Draw(ref text);
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
    public void ContextMenuFitsInsideInputSurface()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        var previousFont = Now.defaultFont;
        var font = ScriptableObject.CreateInstance<NowFont>();

        try
        {
            SetRenderer(theme, renderer);
            Now.defaultFont = font;
            var bottomRight = new Vector2(Surface.x - 2f, Surface.y - 2f);
            _pointer.snapshot = new NowInputSnapshot(
                bottomRight,
                NowPointerButtons.Secondary,
                NowPointerButtons.Secondary,
                NowPointerButtons.None);

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                NowContextMenu.Open(7002, bottomRight);

                if (NowContextMenu.Begin(7002))
                {
                    NowContextMenu.Item("Copy");
                    NowContextMenu.Item("Select All");
                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }

            Assert.GreaterOrEqual(renderer.contextMenuItems, 2);
            Assert.GreaterOrEqual(renderer.lastMenuPopupRect.width, 160f);
            Assert.GreaterOrEqual(renderer.lastMenuPopupRect.x, 0f);
            Assert.GreaterOrEqual(renderer.lastMenuPopupRect.y, 0f);
            Assert.LessOrEqual(renderer.lastMenuPopupRect.xMax, Surface.x);
            Assert.LessOrEqual(renderer.lastMenuPopupRect.yMax, Surface.y);
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
    public void ThemePresetContextMenusStayCompact()
    {
        string[] paths =
        {
            "Assets/NowUI/Assets/Themes/Default.asset",
            "Assets/NowUI/Assets/Themes/DefaultDark.asset",
            "Assets/NowUI/Assets/Themes/Material.asset",
            "Assets/NowUI/Assets/Themes/MaterialDark.asset"
        };

        foreach (string path in paths)
        {
            var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(path);

            Assert.IsNotNull(theme, path);
            Assert.LessOrEqual(
                theme.controlStyles.contextMenuItemHeight,
                32f,
                $"{path} should keep context menu rows compact; large list rows make one-item menus look empty.");
        }
    }

    [Test]
    public void ContextMenuSubmenuDeliversNestedItemClick()
    {
        const int menuId = 7011;
        var anchor = new Vector2(20f, 20f);
        bool clicked = false;

        void DrawFrame(bool open = false)
        {
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor);

                if (NowContextMenu.Begin(menuId))
                {
                    if (NowContextMenu.BeginSubmenu("More"))
                    {
                        if (NowContextMenu.Item("Nested Action"))
                            clicked = true;

                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }
        }

        var styles = NowTheme.themeAsset.controlStyles;
        float rootWidth = Mathf.Max(160f, styles.contextMenuMinWidth);
        var submenuPoint = new Vector2(
            anchor.x + styles.popupPadding + 12f,
            anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);
        var nestedPoint = new Vector2(anchor.x + rootWidth + 12f, submenuPoint.y);

        _pointer.snapshot = new NowInputSnapshot(submenuPoint, false, false, false);
        DrawFrame(open: true);

        _pointer.snapshot = new NowInputSnapshot(nestedPoint, true, true, false);
        DrawFrame();

        _pointer.snapshot = new NowInputSnapshot(nestedPoint, false, false, true);
        DrawFrame();

        Assert.IsFalse(clicked, "Context menu item clicks are delivered on the owner draw after the overlay closes.");

        _pointer.snapshot = new NowInputSnapshot(nestedPoint, false, false, false);
        DrawFrame();

        Assert.IsTrue(clicked);
        Assert.IsFalse(NowContextMenu.isOpen);
    }

    [Test]
    public void ContextMenuSubmenuSurvivesDiagonalHoverThroughSiblingRows()
    {
        const int menuId = 7013;
        var anchor = new Vector2(20f, 20f);

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        int DrawFrame(Vector2 pointer, float time, bool open = false)
        {
            _pointer.snapshot = new NowInputSnapshot(
                true, pointer, pointer, Vector2.zero,
                NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
                Vector2.zero, Vector2.zero,
                false, false, false, false, false, false, false, false,
                1, time);

            int before = renderer.popupBackgrounds;

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor);

                if (NowContextMenu.Begin(menuId))
                {
                    if (NowContextMenu.BeginSubmenu("More"))
                    {
                        NowContextMenu.Item("Nested Action");
                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.Item("Last");
                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }

            return renderer.popupBackgrounds - before;
        }

        try
        {
            var styles = theme.controlStyles;
            float rowX = anchor.x + styles.popupPadding + 12f;
            var submenuRow = new Vector2(rowX, anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);
            var lastRow = new Vector2(rowX, anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 1.5f);

            DrawFrame(submenuRow, 1.0f, open: true);
            Assert.AreEqual(2, DrawFrame(submenuRow, 1.05f), "Hovering the submenu row must open its child menu.");
            Assert.AreEqual(2, DrawFrame(lastRow, 1.1f), "Briefly crossing a sibling row must not snap the submenu shut.");
            DrawFrame(lastRow, 1.4f);
            Assert.AreEqual(1, DrawFrame(lastRow, 1.45f), "Dwelling on a sibling row past the hover-intent delay closes the submenu.");
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void OverlayPointerBlocksAreScopedToTheirHost()
    {
        var hostObject = new GameObject("Overlay Host Test", typeof(Camera));
        var host = hostObject.GetComponent<Camera>();
        var otherObject = new GameObject("Other Overlay Host Test", typeof(Camera));
        var other = otherObject.GetComponent<Camera>();
        var point = new Vector2(50f, 50f);

        try
        {
            using (NowInput.Begin(_pointer, Surface))
            {
                using (NowOverlay.Host(host))
                    NowOverlay.BlockScreen(new NowRect(0f, 0f, 200f, 200f));

                NowOverlay.ForceNewFrame();

                Assert.IsFalse(
                    NowOverlay.IsPointerBlocked(point),
                    "A block registered under a host must not block hostless surfaces.");

                using (NowOverlay.Host(other))
                {
                    Assert.IsFalse(
                        NowOverlay.IsPointerBlocked(point),
                        "A block registered under one host must not block another host.");
                }

                using (NowOverlay.Host(host))
                {
                    Assert.IsTrue(
                        NowOverlay.IsPointerBlocked(point),
                        "A block must still apply to its own host.");
                }

                using (NowOverlay.Host(host))
                    NowOverlay.BlockAllSurfaces();

                NowOverlay.ForceNewFrame();

                Assert.IsTrue(
                    NowOverlay.IsPointerBlocked(point),
                    "A modal block must apply to hostless surfaces.");

                using (NowOverlay.Host(other))
                {
                    Assert.IsTrue(
                        NowOverlay.IsPointerBlocked(point),
                        "A modal block must apply across hosts.");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(otherObject);
        }
    }

    static int s_nestedPopupClicks;
    static bool s_nestedMenuDelivered;
    const int NestedPopupOverlayId = 8242;
    const int NestedMenuId = 8123;
    static readonly NowRect NestedPopupRect = new NowRect(40f, 40f, 220f, 160f);

    static void DrawNestedPopupOverlay(int state)
    {
        var interaction = NowInput.Interact(NowInput.CombineId(NestedPopupOverlayId, 1), NestedPopupRect);

        if (interaction.clicked)
            ++s_nestedPopupClicks;

        var context = NowInput.Interact(NowInput.CombineId(NestedPopupOverlayId, 2), NestedPopupRect, NowPointerButton.Secondary);

        if (context.clicked)
            NowContextMenu.Open(NestedMenuId, context.pointerPosition, fitToView: false);

        if (NowContextMenu.Begin(NestedMenuId))
        {
            if (NowContextMenu.Item("Smooth Tangents"))
                s_nestedMenuDelivered = true;

            NowContextMenu.End();
        }
    }

    /// <summary>
    /// A context menu opened from inside another overlay popup (the animation
    /// curve editor's tangent menu) must win the pointer over the popup content
    /// beneath it, even where the two overlap.
    /// </summary>
    [Test]
    public void ContextMenuOpenedInsideOverlayWinsOverThePopupBeneath()
    {
        s_nestedPopupClicks = 0;
        s_nestedMenuDelivered = false;

        void DrawFrame(NowInputSnapshot snapshot)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;

            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                NowOverlay.Defer(NestedPopupRect, NestedPopupOverlayId, DrawNestedPopupOverlay);
                NowOverlay.Flush();
            }
        }

        var styles = NowTheme.themeAsset.controlStyles;
        var anchor = new Vector2(100f, 60f);
        var itemPoint = new Vector2(
            anchor.x + styles.popupPadding + 10f,
            anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);

        Assert.IsTrue(NestedPopupRect.Contains(itemPoint), "The probe point must overlap the popup beneath the menu.");

        DrawFrame(new NowInputSnapshot(anchor, false, false, false));
        DrawFrame(new NowInputSnapshot(anchor, NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None));
        DrawFrame(new NowInputSnapshot(anchor, NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.Secondary));

        Assert.IsTrue(NowContextMenu.isOpen, "Right-click inside the popup must open the nested context menu.");

        DrawFrame(new NowInputSnapshot(itemPoint, false, false, false));
        DrawFrame(new NowInputSnapshot(itemPoint, true, true, false));
        DrawFrame(new NowInputSnapshot(itemPoint, false, false, true));
        DrawFrame(new NowInputSnapshot(itemPoint, false, false, false));

        Assert.IsTrue(s_nestedMenuDelivered, "The nested context menu item must receive the click.");
        Assert.AreEqual(0, s_nestedPopupClicks, "The popup beneath the nested menu must not receive the click.");
    }

    [Test]
    public void OverlayFitToViewAccountsForCurrentTransform()
    {
        using (NowInput.Begin(_pointer, Surface))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        {
            var rect = new NowRect(240f, 110f, 80f, 40f);
            var fitted = NowOverlay.FitToView(rect);
            var screen = Now.TransformScreenRect(fitted);

            Assert.GreaterOrEqual(screen.x, 0f);
            Assert.GreaterOrEqual(screen.y, 0f);
            Assert.LessOrEqual(screen.xMax, Surface.x);
            Assert.LessOrEqual(screen.yMax, Surface.y);
        }
    }

    [Test]
    public void DropdownPopupFitsInsideInputSurfaceByDefault()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        var options = new List<string> { "Low", "Medium", "High" };
        int selected = 0;
        var rect = new NowRect(Surface.x - 84f, Surface.y - 26f, 96f, 24f);
        int id = NowControls.ResolveNavigationTargetId("quality-fit");
        NowControlState.Get<bool>(id) = true;

        try
        {
            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                Now.Dropdown(rect, "quality-fit", options).Draw(ref selected);
                NowOverlay.Flush();
            }

            Assert.Greater(renderer.popupBackgrounds, 0);
            Assert.GreaterOrEqual(renderer.lastPopupRect.x, 0f);
            Assert.GreaterOrEqual(renderer.lastPopupRect.y, 0f);
            Assert.LessOrEqual(renderer.lastPopupRect.xMax, Surface.x);
            Assert.LessOrEqual(renderer.lastPopupRect.yMax, Surface.y);
        }
        finally
        {
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
    public void TextFieldDoubleClickDragExtendsByWholeWords()
    {
        string text = "hello world wide";
        Vector2 insideWorld = TextFieldPoint("hello wo");
        Vector2 insideWide = TextFieldPoint("hello world wi");

        PointerFrame(ref text, insideWorld, down: true, pressed: true, released: false);
        PointerFrame(ref text, insideWorld, down: false, pressed: false, released: true);
        PointerFrame(ref text, insideWorld, down: true, pressed: true, released: false);
        PointerFrame(ref text, insideWide, down: true, pressed: false, released: false);
        PointerFrame(ref text, insideWide, down: false, pressed: false, released: true);

        Assert.AreEqual("world wide", NowTextEdit.GetSelection(text, FieldState()),
            "Dragging after a double-click stays in word selection mode.");
    }

    [Test]
    public void TextFieldTripleClickSelectsLine()
    {
        string text = "hello world";
        Vector2 click = TextFieldPoint("hello");

        for (int i = 0; i < 3; ++i)
        {
            PointerFrame(ref text, click, down: true, pressed: true, released: false);
            PointerFrame(ref text, click, down: false, pressed: false, released: true);
        }

        Assert.AreEqual(text, NowTextEdit.GetSelection(text, FieldState()));
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
    public void NestedTransformsComposeForInput()
    {
        _pointer.snapshot = new NowInputSnapshot(new Vector2(27f, 14f), false, false, false);

        using (NowInput.Begin(_pointer, Surface))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        using (Now.Transform(3f, new Vector2(4f, 1f)))
        {
            Assert.AreEqual(6f, Now.currentTransform.scale.x, 0.0001f);
            Assert.AreEqual(18f, Now.currentTransform.origin.x, 0.0001f);
            Assert.AreEqual(7f, Now.currentTransform.origin.y, 0.0001f);
            Assert.IsTrue(NowInput.IsHovered(new NowRect(1f, 1f, 2f, 2f)));
        }

        Assert.AreEqual(1f, Now.currentTransform.scale.x, 0.0001f);
    }

    [Test]
    public void OverlayDeferredDrawRestoresCapturedTransform()
    {
        const int overlayId = 404;
        bool ran = false;
        bool hovered = false;
        bool insideTree = false;
        Vector2 seenScale = default;
        Vector2 localPointer = default;
        Vector2 pointer = new Vector2(25f, 20f);
        _pointer.snapshot = new NowInputSnapshot(pointer, false, false, false);

        void DrawOverlay(int id)
        {
            ran = true;
            seenScale = Now.currentTransform.scale;
            var interaction = NowInput.Interact(409, new NowRect(5f, 5f, 10f, 10f));
            hovered = interaction.hovered;
            localPointer = interaction.pointerPosition;
            insideTree = NowOverlay.IsPointerInsideOverlayTree(id, pointer);
            Now.Rectangle(new NowRect(5f, 5f, 10f, 10f)).SetColor(Color.red).Draw();
        }

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            using (Now.Transform(2f, new Vector2(10f, 5f)))
                NowOverlay.Defer(new NowRect(5f, 5f, 10f, 10f), overlayId, DrawOverlay);

            Assert.IsFalse(ran, "Deferred draws must not run inline.");
        }

        Assert.IsTrue(ran);
        Assert.AreEqual(2f, seenScale.x, 0.0001f);
        Assert.AreEqual(2f, seenScale.y, 0.0001f);
        Assert.IsTrue(hovered);
        Assert.AreEqual(new Vector2(7.5f, 7.5f), localPointer);
        Assert.IsTrue(insideTree);
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
    public void OverlayTracksNestedPopupTree()
    {
        const int parentId = 101;
        const int childId = 202;
        var parentRect = new NowRect(0, 0, 100, 100);
        var childRect = new NowRect(120, 0, 80, 80);
        var childPoint = new Vector2(140, 20);
        bool parentSawChild = false;
        bool parentHadChild = false;
        bool childSawSelf = false;

        void DrawParent(int id)
        {
            NowOverlay.Defer(childRect, childId, DrawChild);
            parentSawChild = NowOverlay.IsPointerInsideOverlayTree(id, childPoint);
            parentHadChild = NowOverlay.HasNestedOverlay(id);
        }

        void DrawChild(int id)
        {
            childSawSelf = NowOverlay.IsPointerInsideOverlayTree(id, childPoint);
        }

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowOverlay.Defer(parentRect, parentId, DrawParent);
        }

        Assert.IsTrue(parentSawChild);
        Assert.IsTrue(parentHadChild);
        Assert.IsTrue(childSawSelf);
        Assert.IsFalse(NowOverlay.IsPointerInsideOverlayTree(parentId, new Vector2(300, 300)));
    }

    [Test]
    public void OverlayConcretePopupHitTestIgnoresModalScreenBlock()
    {
        using (NowInput.Begin(_pointer, Surface))
        {
            NowOverlay.BlockScreen(new NowRect(-1000f, -1000f, 2000f, 2000f));
            NowOverlay.DeferScreen(new NowRect(40f, 30f, 80f, 50f), 303, _ => { });

            Assert.IsTrue(NowOverlay.IsPointerInsideOverlay(new Vector2(60f, 40f)));
            Assert.IsFalse(NowOverlay.IsPointerInsideOverlay(new Vector2(200f, 200f)));
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
                NowLayout.Rect(180, 30);
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
                NowLayout.Rect(180, 30);
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
                    NowLayout.Rect(260f, 40f);
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
                    NowLayout.Rect(195f, 130f);
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
                    NowLayout.Rect(260f, 40f);
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

        NowControlState.Get<int>(dropdownId, "pending") = 3;

        _pointer.snapshot = default;
        bool changed;

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            changed = Now.Dropdown(rect, "quality", options).Draw(ref selected);

        Assert.IsTrue(changed);
        Assert.AreEqual(2, selected, "Pending selection 3 maps to index 2.");
    }

    [Test]
    public void DropdownPopupSelectsItemInsideTransform()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        var options = new List<string> { "Low", "Medium", "High" };
        int selected = 0;
        var rect = new NowRect(20, 20, 160, 30);
        var transformOrigin = new Vector2(10f, 5f);
        const float scale = 2f;

        Vector2 ToScreen(Vector2 local)
        {
            return local * scale + transformOrigin;
        }

        bool DrawFrame()
        {
            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            using (Now.Transform(scale, transformOrigin))
            {
                return Now.Dropdown(rect, "quality-transform", options)
                    .SetFitToView(false)
                    .Draw(ref selected);
            }
        }

        try
        {
            _pointer.snapshot = new NowInputSnapshot(ToScreen(new Vector2(30f, 30f)), true, true, false);
            DrawFrame();

            _pointer.snapshot = new NowInputSnapshot(ToScreen(new Vector2(30f, 30f)), false, false, true);
            DrawFrame();

            Assert.Greater(renderer.popupBackgrounds, 0, "Opened dropdown must draw its popup through the captured transform.");

            var styles = theme.controlStyles;
            var secondItem = new Vector2(
                rect.x + 16f,
                rect.yMax + styles.dropdownPopupGap + styles.popupPadding + styles.dropdownItemHeight * 1.5f);
            var secondItemScreen = ToScreen(secondItem);

            _pointer.snapshot = new NowInputSnapshot(secondItemScreen, true, true, false);
            DrawFrame();

            _pointer.snapshot = new NowInputSnapshot(secondItemScreen, false, false, true);
            DrawFrame();

            _pointer.snapshot = default;
            bool changed = DrawFrame();

            Assert.IsTrue(changed);
            Assert.AreEqual(1, selected);
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }
}
