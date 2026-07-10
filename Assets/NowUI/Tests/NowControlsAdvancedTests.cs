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

    sealed class RecordedContextMenuItem
    {
        public string label;
        public NowRect rect;
        public bool selected;
        public bool hasSubmenu;
        public bool submenuOpen;

        public RecordedContextMenuItem(string label, NowRect rect, bool selected, bool hasSubmenu)
        {
            this.label = label;
            this.rect = rect;
            this.selected = selected;
            this.hasSubmenu = hasSubmenu;
            submenuOpen = false;
        }
    }

    readonly struct ContextMenuFrame
    {
        public readonly int popupCount;
        public readonly List<string> labels;
        public readonly List<NowRect> popupRects;
        public readonly List<RecordedContextMenuItem> items;

        public ContextMenuFrame(
            int popupCount,
            List<string> labels,
            List<NowRect> popupRects,
            List<RecordedContextMenuItem> items)
        {
            this.popupCount = popupCount;
            this.labels = labels;
            this.popupRects = popupRects;
            this.items = items;
        }

        public bool ContainsLabel(string label)
        {
            return labels.Contains(label);
        }

        public RecordedContextMenuItem Item(string label)
        {
            for (int i = 0; i < items.Count; ++i)
            {
                if (items[i].label == label)
                    return items[i];
            }

            Assert.Fail($"Expected context menu item '{label}' to be drawn.");
            return default;
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
        public readonly List<string> popupLabels = new List<string>();
        public readonly List<string> popupDetails = new List<string>();
        public readonly List<string> contextMenuLabels = new List<string>();
        public readonly List<NowRect> menuPopupRects = new List<NowRect>();
        public readonly List<RecordedContextMenuItem> contextMenuItemRecords = new List<RecordedContextMenuItem>();
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
            {
                lastMenuPopupRect = rect;
                menuPopupRects.Add(rect);
            }

            base.DrawPopupBackground(themeAsset, rect, menu);
        }

        public override void DrawPopupItem(in NowPopupItemRenderContext context)
        {
            ++popupItems;
            popupLabels.Add(context.label);
            popupDetails.Add(context.detail);
            base.DrawPopupItem(context);
        }

        public override void DrawContextMenuItem(in NowPopupItemRenderContext context)
        {
            ++contextMenuItems;
            contextMenuLabels.Add(context.label);
            contextMenuItemRecords.Add(new RecordedContextMenuItem(
                context.label,
                context.rect,
                context.selected,
                context.hasSubmenu));
            base.DrawContextMenuItem(context);
        }

        public override void DrawContextMenuSubmenuIndicator(NowThemeAsset themeAsset, NowRect rect, bool enabled, bool open)
        {
            for (int i = contextMenuItemRecords.Count - 1; i >= 0; --i)
            {
                var item = contextMenuItemRecords[i];

                if (item.hasSubmenu && RectsMatch(item.rect, rect))
                {
                    item.submenuOpen = open;
                    break;
                }
            }

            base.DrawContextMenuSubmenuIndicator(themeAsset, rect, enabled, open);
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

    static bool RectsMatch(NowRect a, NowRect b)
    {
        return Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    static NowInputSnapshot PointerSnapshot(Vector2 pointer, float time)
    {
        return new NowInputSnapshot(
            true, pointer, pointer, Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            false, false, false, false, false, false, false, false,
            1, time);
    }

    static NowInputSnapshot ComboSnapshot(
        Vector2 pointer,
        bool down = false,
        bool pressed = false,
        bool released = false,
        bool submitPressed = false)
    {
        return new NowInputSnapshot(
            true,
            pointer,
            pointer,
            Vector2.zero,
            NowInputSnapshot.ToButtonMask(down, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(pressed, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(released, NowPointerButton.Primary),
            Vector2.zero,
            Vector2.zero,
            false,
            false,
            submitPressed,
            submitPressed,
            false,
            false,
            false,
            false,
            Time.frameCount,
            Time.realtimeSinceStartup);
    }

    ContextMenuFrame DrawSiblingSubmenuFrame(
        NowThemeAsset theme,
        RecordingRenderer renderer,
        int menuId,
        Vector2 anchor,
        Vector2 pointer,
        float time,
        bool open = false,
        bool tallFirstSubmenu = false)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = PointerSnapshot(pointer, time);

        int popupBefore = renderer.popupBackgrounds;
        int labelBefore = renderer.contextMenuLabels.Count;
        int rectBefore = renderer.menuPopupRects.Count;
        int itemBefore = renderer.contextMenuItemRecords.Count;

        using (NowTheme.Scope(theme))
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            if (open)
                NowContextMenu.Open(menuId, anchor);

            if (NowContextMenu.Begin(menuId))
            {
                if (NowContextMenu.BeginSubmenu("First"))
                {
                    NowContextMenu.Item("First Child A");

                    if (tallFirstSubmenu)
                    {
                        NowContextMenu.Item("First Child B");
                        NowContextMenu.Item("First Child C");
                        NowContextMenu.Item("First Child D");
                    }

                    NowContextMenu.EndSubmenu();
                }

                if (NowContextMenu.BeginSubmenu("Second"))
                {
                    NowContextMenu.Item("Second Child");
                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.End();
            }

            NowOverlay.Flush();
        }

        return new ContextMenuFrame(
            renderer.popupBackgrounds - popupBefore,
            renderer.contextMenuLabels.GetRange(labelBefore, renderer.contextMenuLabels.Count - labelBefore),
            renderer.menuPopupRects.GetRange(rectBefore, renderer.menuPopupRects.Count - rectBefore),
            renderer.contextMenuItemRecords.GetRange(itemBefore, renderer.contextMenuItemRecords.Count - itemBefore));
    }

    ContextMenuFrame DrawNestedSubmenuFrame(
        NowThemeAsset theme,
        RecordingRenderer renderer,
        int menuId,
        Vector2 anchor,
        Vector2 pointer,
        float time,
        bool open = false)
    {
        NowOverlay.ForceNewFrame();
        _pointer.snapshot = PointerSnapshot(pointer, time);

        int popupBefore = renderer.popupBackgrounds;
        int labelBefore = renderer.contextMenuLabels.Count;
        int rectBefore = renderer.menuPopupRects.Count;
        int itemBefore = renderer.contextMenuItemRecords.Count;

        using (NowTheme.Scope(theme))
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            if (open)
                NowContextMenu.Open(menuId, anchor);

            if (NowContextMenu.Begin(menuId))
            {
                if (NowContextMenu.BeginSubmenu("Level 1"))
                {
                    if (NowContextMenu.BeginSubmenu("Level 2"))
                    {
                        NowContextMenu.Item("Deep Action");
                        NowContextMenu.Item("Deep Settings");
                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.Item("Level 1 Action");
                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.Item("Root Action");
                NowContextMenu.End();
            }

            NowOverlay.Flush();
        }

        return new ContextMenuFrame(
            renderer.popupBackgrounds - popupBefore,
            renderer.contextMenuLabels.GetRange(labelBefore, renderer.contextMenuLabels.Count - labelBefore),
            renderer.menuPopupRects.GetRange(rectBefore, renderer.menuPopupRects.Count - rectBefore),
            renderer.contextMenuItemRecords.GetRange(itemBefore, renderer.contextMenuItemRecords.Count - itemBefore));
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
        NowFocus.Focus(NowInput.GetId("name"));
    }

    ref NowTextEditState FieldState()
    {
        return ref NowControlState.Get<NowTextEditState>(NowInput.GetId("name"));
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
    public void ComboBoxPopupMinWidthCanExceedFieldWidth()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        var options = new List<string>
        {
            "Game.MathUtils.Fibonacci(int)",
            "Game.MathUtils.TextScore(string)",
            "Game.Rules.IsCritical(bool)"
        };
        int selected = 0;
        var rect = new NowRect(20f, 20f, 140f, 30f);
        const float minPopupWidth = 320f;

        void DrawFrame(NowInputSnapshot snapshot)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                Now.ComboBox(rect, options)
                    .SetId("wide-combo")
                    .SetPopupMinWidth(minPopupWidth)
                    .Draw(ref selected);
                NowOverlay.Flush();
            }
        }

        try
        {
            DrawFrame(new NowInputSnapshot(rect.center, true, true, false));
            DrawFrame(new NowInputSnapshot(rect.center, false, false, true));
            DrawFrame(new NowInputSnapshot(rect.center, false, false, false));

            Assert.Greater(renderer.popupBackgrounds, 0);
            Assert.Greater(renderer.lastPopupRect.width, rect.width);
            Assert.GreaterOrEqual(renderer.lastPopupRect.width, minPopupWidth - 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ComboBoxOptionDetailsRenderAndFilter()
    {
        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        var options = new List<string>
        {
            "TextScore(string)",
            "FlagScore(bool)"
        };
        var details = new List<string>
        {
            "int Game.MathUtils.TextScore(string)",
            "int Game.MathUtils.FlagScore(bool)"
        };
        int selected = 1;
        var rect = new NowRect(20f, 20f, 140f, 30f);

        bool DrawFrame(NowInputSnapshot snapshot, string typed = null)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;
            _keyboard.frame = new NowTextInputFrame { characters = typed };
            NowTextInput.Invalidate();

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                bool changed = Now.ComboBox(rect, options)
                    .SetId("detail-combo")
                    .SetPopupMinWidth(320f)
                    .SetOptionDetails(details)
                    .Draw(ref selected);
                NowOverlay.Flush();
                return changed;
            }
        }

        try
        {
            Vector2 center = rect.center;
            DrawFrame(ComboSnapshot(center, down: true, pressed: true));
            DrawFrame(ComboSnapshot(center, released: true));
            DrawFrame(ComboSnapshot(center));
            DrawFrame(ComboSnapshot(center));

            Assert.That(renderer.popupDetails, Does.Contain(details[0]));
            Assert.That(renderer.popupDetails, Does.Contain(details[1]));

            DrawFrame(ComboSnapshot(center), "Game.MathUtils.TextScore");
            DrawFrame(ComboSnapshot(center, submitPressed: true));

            Assert.IsTrue(DrawFrame(ComboSnapshot(center)));
            Assert.AreEqual(0, selected);
        }
        finally
        {
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
    public void ContextMenuClickDeliversByLabelWhenItemsShift()
    {
        const int menuId = 7017;
        var anchor = new Vector2(20f, 20f);
        bool includeCopy = true;
        bool copyClicked = false;
        bool selectAllClicked = false;

        void DrawFrame(bool open = false)
        {
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor);

                if (NowContextMenu.Begin(menuId))
                {
                    if (includeCopy && NowContextMenu.Item("Copy"))
                        copyClicked = true;

                    if (NowContextMenu.Item("Select All"))
                        selectAllClicked = true;

                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }
        }

        void ClickCopyRow()
        {
            var styles = NowTheme.themeAsset.controlStyles;
            var copyRow = new Vector2(
                anchor.x + styles.popupPadding + 12f,
                anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);

            _pointer.snapshot = new NowInputSnapshot(copyRow, false, false, false);
            DrawFrame(open: true);

            _pointer.snapshot = new NowInputSnapshot(copyRow, true, true, false);
            DrawFrame();

            _pointer.snapshot = new NowInputSnapshot(copyRow, false, false, true);
            DrawFrame();
        }

        ClickCopyRow();
        includeCopy = false;
        _pointer.snapshot = new NowInputSnapshot(new Vector2(20f, 20f), false, false, false);
        DrawFrame();

        Assert.IsFalse(selectAllClicked,
            "a click on Copy must not deliver to whichever item slid into its slot on the delivery frame");
        Assert.IsFalse(copyClicked, "an undeclared item cannot deliver");

        includeCopy = true;
        ClickCopyRow();
        _pointer.snapshot = new NowInputSnapshot(new Vector2(20f, 20f), false, false, false);
        DrawFrame();

        Assert.IsTrue(copyClicked, "the clicked label delivers when it is declared on the delivery frame");
        Assert.IsFalse(selectAllClicked);
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
            _pointer.snapshot = PointerSnapshot(pointer, time);

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
    public void ContextMenuHoverSwitchesBetweenSiblingSubmenus()
    {
        const int menuId = 7311;
        var anchor = new Vector2(20f, 20f);

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var styles = theme.controlStyles;
            float rowX = anchor.x + styles.popupPadding + 12f;
            var firstRow = new Vector2(rowX, anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);
            var secondRow = new Vector2(rowX, anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 1.5f);

            DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, firstRow, 1.0f, open: true);

            var firstOpen = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, firstRow, 1.05f);
            Assert.AreEqual(2, firstOpen.popupCount, "Hovering the first submenu row must open its child menu.");
            Assert.AreEqual(2, firstOpen.popupRects.Count);
            Assert.IsTrue(firstOpen.ContainsLabel("First Child A"));
            Assert.IsFalse(firstOpen.ContainsLabel("Second Child"));
            Assert.IsTrue(firstOpen.Item("First").submenuOpen);
            Assert.IsFalse(firstOpen.Item("Second").submenuOpen);
            Assert.Greater(firstOpen.popupRects[1].xMax, firstOpen.popupRects[0].xMax, "The child menu should extend beyond the root menu.");

            var switchingToSecond = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, secondRow, 1.1f);
            Assert.AreEqual(1, switchingToSecond.popupCount, "Switching sibling submenu rows must not leave the old child queued for one more frame.");
            Assert.IsFalse(switchingToSecond.ContainsLabel("First Child A"));
            Assert.IsFalse(switchingToSecond.ContainsLabel("Second Child"));
            Assert.IsFalse(switchingToSecond.Item("First").submenuOpen);
            Assert.IsTrue(switchingToSecond.Item("Second").submenuOpen);

            var secondOpen = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, secondRow, 1.15f);
            Assert.AreEqual(2, secondOpen.popupCount, "The newly-hovered submenu should draw on the next declaration frame.");
            Assert.IsFalse(secondOpen.ContainsLabel("First Child A"));
            Assert.IsTrue(secondOpen.ContainsLabel("Second Child"));
            Assert.IsFalse(secondOpen.Item("First").submenuOpen);
            Assert.IsTrue(secondOpen.Item("Second").submenuOpen);

            var switchingToFirst = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, firstRow, 1.2f);
            Assert.AreEqual(1, switchingToFirst.popupCount, "Hovering back over a previous submenu row must retire the current child immediately.");
            Assert.IsFalse(switchingToFirst.ContainsLabel("First Child A"));
            Assert.IsFalse(switchingToFirst.ContainsLabel("Second Child"));
            Assert.IsTrue(switchingToFirst.Item("First").submenuOpen);
            Assert.IsFalse(switchingToFirst.Item("Second").submenuOpen);

            var firstOpenAgain = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, firstRow, 1.25f);
            Assert.AreEqual(2, firstOpenAgain.popupCount);
            Assert.IsTrue(firstOpenAgain.ContainsLabel("First Child A"));
            Assert.IsFalse(firstOpenAgain.ContainsLabel("Second Child"));
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ContextMenuKeepsSubmenuOpenWhenAimingThroughSiblingSubmenuRow()
    {
        const int menuId = 7312;
        var anchor = new Vector2(20f, 20f);

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var styles = theme.controlStyles;
            float rowX = anchor.x + styles.popupPadding + 12f;
            float aimingX = anchor.x + styles.popupPadding + 70f;
            var firstRow = new Vector2(rowX, anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);
            var secondRowAim = new Vector2(aimingX, anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 1.5f);

            DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, firstRow, 1.0f, open: true, tallFirstSubmenu: true);
            var firstOpen = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, firstRow, 1.05f, tallFirstSubmenu: true);
            Assert.AreEqual(2, firstOpen.popupCount, "Hovering the first submenu row must open its child menu.");
            Assert.IsTrue(firstOpen.ContainsLabel("First Child A"));
            Assert.IsFalse(firstOpen.ContainsLabel("Second Child"));
            Assert.IsTrue(firstOpen.Item("First").submenuOpen);
            Assert.IsFalse(firstOpen.Item("Second").submenuOpen);

            var aimingAcrossSecond = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, secondRowAim, 1.1f, tallFirstSubmenu: true);
            Assert.AreEqual(2, aimingAcrossSecond.popupCount, "Moving diagonally toward the open child through a sibling submenu row must keep the current child open.");
            Assert.IsTrue(aimingAcrossSecond.ContainsLabel("First Child A"));
            Assert.IsFalse(aimingAcrossSecond.ContainsLabel("Second Child"));
            Assert.IsTrue(aimingAcrossSecond.Item("First").submenuOpen);
            Assert.IsFalse(aimingAcrossSecond.Item("Second").submenuOpen);

            var beforeIntentDelay = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, secondRowAim, 1.15f, tallFirstSubmenu: true);
            Assert.AreEqual(2, beforeIntentDelay.popupCount, "Stopping briefly on the sibling row should still honor the diagonal hover-intent delay.");
            Assert.IsTrue(beforeIntentDelay.ContainsLabel("First Child A"));
            Assert.IsFalse(beforeIntentDelay.ContainsLabel("Second Child"));

            var afterIntentDelay = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, secondRowAim, 1.31f, tallFirstSubmenu: true);
            Assert.AreEqual(1, afterIntentDelay.popupCount, "Dwelling on the sibling submenu row past hover intent switches the selected row without drawing a stale child.");
            Assert.IsFalse(afterIntentDelay.ContainsLabel("First Child A"));
            Assert.IsFalse(afterIntentDelay.ContainsLabel("Second Child"));
            Assert.IsFalse(afterIntentDelay.Item("First").submenuOpen);
            Assert.IsTrue(afterIntentDelay.Item("Second").submenuOpen);

            var secondOpen = DrawSiblingSubmenuFrame(theme, renderer, menuId, anchor, secondRowAim, 1.36f, tallFirstSubmenu: true);
            Assert.AreEqual(2, secondOpen.popupCount);
            Assert.IsFalse(secondOpen.ContainsLabel("First Child A"));
            Assert.IsTrue(secondOpen.ContainsLabel("Second Child"));
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ContextMenuSubmenuFlipsLeftNearRightEdge()
    {
        const int menuId = 7313;
        var anchor = new Vector2(320f, 40f);

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var styles = theme.controlStyles;
            var rootRow = new Vector2(
                anchor.x + styles.popupPadding + 12f,
                anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);

            DrawNestedSubmenuFrame(theme, renderer, menuId, anchor, rootRow, 1.0f, open: true);
            var frame = DrawNestedSubmenuFrame(theme, renderer, menuId, anchor, rootRow, 1.05f);

            Assert.AreEqual(2, frame.popupCount, "The first submenu should be open.");
            Assert.AreEqual(2, frame.popupRects.Count);

            var root = frame.popupRects[0];
            var child = frame.popupRects[1];
            Assert.Less(child.center.x, root.center.x, "A submenu that cannot fit on the right should open to the left when there is room.");
            Assert.GreaterOrEqual(child.x, 0f);
            Assert.LessOrEqual(child.xMax, Surface.x);
            Assert.IsTrue(frame.ContainsLabel("Level 1 Action"));
            Assert.IsTrue(frame.Item("Level 1").submenuOpen);
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ContextMenuNestedSubmenuPingPongsBackRightWhenLeftCannotFit()
    {
        const int menuId = 7314;
        var anchor = new Vector2(250f, 40f);

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        try
        {
            var styles = theme.controlStyles;
            var rootRow = new Vector2(
                anchor.x + styles.popupPadding + 12f,
                anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);

            DrawNestedSubmenuFrame(theme, renderer, menuId, anchor, rootRow, 1.0f, open: true);
            var level1Frame = DrawNestedSubmenuFrame(theme, renderer, menuId, anchor, rootRow, 1.05f);

            Assert.AreEqual(2, level1Frame.popupRects.Count);
            var root = level1Frame.popupRects[0];
            var level1 = level1Frame.popupRects[1];
            Assert.Less(level1.center.x, root.center.x, "The first submenu should flip left from the root.");

            var level2Row = new Vector2(
                level1.x + styles.popupPadding + 12f,
                level1.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);

            DrawNestedSubmenuFrame(theme, renderer, menuId, anchor, level2Row, 1.1f);
            var pingPong = DrawNestedSubmenuFrame(theme, renderer, menuId, anchor, level2Row, 1.15f);

            Assert.AreEqual(3, pingPong.popupCount, "The nested submenu should be open after its declaration frame.");
            Assert.AreEqual(3, pingPong.popupRects.Count);

            root = pingPong.popupRects[0];
            level1 = pingPong.popupRects[1];
            var level2 = pingPong.popupRects[2];
            Assert.Less(level1.center.x, root.center.x, "The first submenu should remain left of the root.");
            Assert.Greater(level2.center.x, level1.center.x, "The deeper submenu should flip back right once the left side no longer fits.");
            Assert.LessOrEqual(level2.xMax, Surface.x);
            Assert.IsTrue(pingPong.ContainsLabel("Deep Action"));
            Assert.IsTrue(pingPong.Item("Level 2").submenuOpen);
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

    static NowInputSnapshot MenuPointer(Vector2 position, Vector2 scroll = default, bool down = false, bool pressed = false, bool released = false)
    {
        return new NowInputSnapshot(
            true, position, position, Vector2.zero,
            NowInputSnapshot.ToButtonMask(down, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(pressed, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(released, NowPointerButton.Primary),
            scroll, Vector2.zero,
            false, false, false, false, false, false, false, false,
            1, 1f);
    }

    [Test]
    public void TallContextMenuClampsToViewAndScrollsToReachEveryItem()
    {
        const int menuId = 7301;
        const int itemCount = 30;
        var anchor = new Vector2(20f, 10f);
        int clickedIndex = -1;

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        void DrawFrame(NowInputSnapshot snapshot, bool open = false)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor, fitToView: true);

                if (NowContextMenu.Begin(menuId))
                {
                    for (int i = 0; i < itemCount; ++i)
                    {
                        if (NowContextMenu.Item("Menu Item"))
                            clickedIndex = i;
                    }

                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }
        }

        try
        {
            var styles = theme.controlStyles;
            float contentHeight = styles.popupPadding * 2f + itemCount * styles.contextMenuItemHeight;

            DrawFrame(MenuPointer(anchor), open: true);

            Assert.Greater(contentHeight, Surface.y, "The fixture must be taller than the view to exercise clamping.");
            Assert.LessOrEqual(renderer.lastMenuPopupRect.height, Surface.y, "An oversized menu must clamp to the view height.");

            var insideMenu = new Vector2(
                renderer.lastMenuPopupRect.x + 20f,
                renderer.lastMenuPopupRect.y + renderer.lastMenuPopupRect.height * 0.5f);

            for (int i = 0; i < 8; ++i)
                DrawFrame(MenuPointer(insideMenu, scroll: new Vector2(0f, -3f)));

            Assert.IsTrue(NowContextMenu.isOpen, "Scrolling inside the menu must not close it.");

            var lastItemPoint = new Vector2(
                renderer.lastMenuPopupRect.x + 20f,
                renderer.lastMenuPopupRect.yMax - styles.popupPadding - styles.contextMenuItemHeight * 0.5f);

            DrawFrame(MenuPointer(lastItemPoint));
            DrawFrame(MenuPointer(lastItemPoint, down: true, pressed: true));
            DrawFrame(MenuPointer(lastItemPoint, released: true));
            DrawFrame(MenuPointer(lastItemPoint));

            Assert.AreEqual(itemCount - 1, clickedIndex, "After scrolling to the bottom, the last item must be clickable.");
            Assert.IsFalse(NowContextMenu.isOpen);
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void ScrollingOutsideAContextMenuClosesIt()
    {
        const int menuId = 7302;
        var anchor = new Vector2(20f, 10f);

        void DrawFrame(NowInputSnapshot snapshot, bool open = false)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;

            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor);

                if (NowContextMenu.Begin(menuId))
                {
                    NowContextMenu.Item("First");
                    NowContextMenu.Item("Second");
                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }
        }

        DrawFrame(MenuPointer(anchor), open: true);
        Assert.IsTrue(NowContextMenu.isOpen);

        DrawFrame(MenuPointer(new Vector2(420f, 220f), scroll: new Vector2(0f, -3f)));

        Assert.IsFalse(NowContextMenu.isOpen, "Scrolling away from the menu must close it.");
    }

    [Test]
    public void RightClickDoesNotLeakThroughAnOpenOverlay()
    {
        var region = new NowRect(20f, 20f, 200f, 40f);
        var inside = new Vector2(60f, 36f);

        bool DrawFrame(NowInputSnapshot snapshot, bool withOverlay)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;
            bool rightClicked;

            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                rightClicked = NowInput.WasRightClicked(region);

                if (withOverlay)
                    NowOverlay.DeferScreen(region, 9401, _ => { });

                NowOverlay.Flush();
            }

            return rightClicked;
        }

        var rightPress = new NowInputSnapshot(
            inside, NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None);

        Assert.IsTrue(
            DrawFrame(rightPress, withOverlay: false),
            "With nothing above it, a right press in the region must report.");

        DrawFrame(new NowInputSnapshot(inside, false, false, false), withOverlay: true);

        Assert.IsFalse(
            DrawFrame(rightPress, withOverlay: true),
            "A right press over an open overlay must not leak to the content beneath it.");
    }

    [Test]
    public void DropdownPopupClosesOnASecondaryPressOutside()
    {
        var options = new List<string> { "Low", "High" };
        int selected = 0;

        void DrawFrame(NowInputSnapshot snapshot)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;

            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                Now.Dropdown(FieldRect, "quality", options).Draw(ref selected);
                NowOverlay.Flush();
            }
        }

        int dropdownId;

        using (NowInput.Begin(_pointer, Surface))
            dropdownId = NowControls.GetControlId("quality");

        NowControlState.Get<bool>(dropdownId) = true;
        var outside = new Vector2(420f, 220f);

        DrawFrame(new NowInputSnapshot(outside, false, false, false));
        Assert.IsTrue(NowControlState.Get<bool>(dropdownId), "The fixture popup must be open before the press.");

        DrawFrame(new NowInputSnapshot(
            outside, NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None));

        Assert.IsFalse(
            NowControlState.Get<bool>(dropdownId),
            "A secondary press outside must dismiss the popup exactly like a primary press.");
    }

    static NowInputSnapshot MenuKeyboard(Vector2 position, Vector2 navigation = default, bool submitPressed = false)
    {
        return new NowInputSnapshot(
            true, position, position, Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, navigation,
            false, false, false, submitPressed, false, false, false, false,
            1, 1f);
    }

    [Test]
    public void ContextMenuKeyboardNavigatesAndActivatesItems()
    {
        const int menuId = 7305;
        var anchor = new Vector2(20f, 10f);
        string clicked = null;

        void DrawFrame(NowInputSnapshot snapshot, bool open = false)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;

            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor);

                if (NowContextMenu.Begin(menuId))
                {
                    if (NowContextMenu.Item("First"))
                        clicked = "First";

                    if (NowContextMenu.Item("Second"))
                        clicked = "Second";

                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }
        }

        var down = new Vector2(0f, -1f);

        DrawFrame(MenuKeyboard(anchor), open: true);
        DrawFrame(MenuKeyboard(anchor, navigation: down));
        DrawFrame(MenuKeyboard(anchor));
        DrawFrame(MenuKeyboard(anchor, navigation: down));
        DrawFrame(MenuKeyboard(anchor));
        DrawFrame(MenuKeyboard(anchor, submitPressed: true));
        DrawFrame(MenuKeyboard(anchor));

        Assert.AreEqual("Second", clicked, "Two down pulses and submit must activate the second item.");
        Assert.IsFalse(NowContextMenu.isOpen, "Keyboard activation closes the menu like a click.");
    }

    [Test]
    public void ContextMenuKeyboardDivesIntoAndOutOfSubmenus()
    {
        const int menuId = 7306;
        var anchor = new Vector2(20f, 10f);
        string clicked = null;

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        int DrawFrame(NowInputSnapshot snapshot, bool open = false)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;
            int before = renderer.popupBackgrounds;

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor);

                if (NowContextMenu.Begin(menuId))
                {
                    NowContextMenu.Item("First");

                    if (NowContextMenu.BeginSubmenu("More"))
                    {
                        if (NowContextMenu.Item("Nested"))
                            clicked = "Nested";

                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }

            return renderer.popupBackgrounds - before;
        }

        try
        {
            var down = new Vector2(0f, -1f);
            var right = new Vector2(1f, 0f);
            var left = new Vector2(-1f, 0f);

            DrawFrame(MenuKeyboard(anchor), open: true);
            DrawFrame(MenuKeyboard(anchor, navigation: down));
            DrawFrame(MenuKeyboard(anchor));
            DrawFrame(MenuKeyboard(anchor, navigation: down));
            DrawFrame(MenuKeyboard(anchor, navigation: right));
            Assert.AreEqual(2, DrawFrame(MenuKeyboard(anchor)), "A right pulse on the submenu row must open its child.");

            DrawFrame(MenuKeyboard(anchor, navigation: left));
            Assert.AreEqual(1, DrawFrame(MenuKeyboard(anchor)), "A left pulse must close the submenu again.");

            DrawFrame(MenuKeyboard(anchor, navigation: right));
            DrawFrame(MenuKeyboard(anchor));
            DrawFrame(MenuKeyboard(anchor, submitPressed: true));
            DrawFrame(MenuKeyboard(anchor));

            Assert.AreEqual("Nested", clicked, "Submit must activate the highlighted submenu item.");
            Assert.IsFalse(NowContextMenu.isOpen);
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    /// <summary>
    /// A submenu clamped over its parent after neither side can fully fit must
    /// own the pointer where they overlap: the covered parent rows must neither
    /// retarget the open path on dwell nor steal the press.
    /// </summary>
    [Test]
    public void SubmenuOverlappingItsParentOwnsThePointer()
    {
        const int menuId = 7307;
        const string wideA = "Wide Submenu Item A Extended";
        const string wideB = "Wide Submenu Item B Extended";
        const string wideC = "Wide Submenu Item C Extended";
        string clicked = null;

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        var styles = theme.controlStyles;
        float itemHeight = styles.contextMenuItemHeight;
        float pad = styles.popupPadding;
        float menuWidth = Mathf.Max(160f, styles.contextMenuMinWidth);
        var anchor = new Vector2(200f, 20f);

        int DrawFrame(Vector2 pointer, float time, bool open = false, bool down = false, bool pressed = false, bool released = false)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = new NowInputSnapshot(
                true, pointer, pointer, Vector2.zero,
                NowInputSnapshot.ToButtonMask(down, NowPointerButton.Primary),
                NowInputSnapshot.ToButtonMask(pressed, NowPointerButton.Primary),
                NowInputSnapshot.ToButtonMask(released, NowPointerButton.Primary),
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
                        NowContextMenu.Item(wideA);

                        if (NowContextMenu.Item(wideB))
                            clicked = "B";

                        NowContextMenu.Item(wideC);
                        NowContextMenu.EndSubmenu();
                    }

                    if (NowContextMenu.Item("Under"))
                        clicked = "Under";

                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }

            return renderer.popupBackgrounds - before;
        }

        try
        {
            var submenuRow = new Vector2(anchor.x + pad + 10f, anchor.y + pad + itemHeight * 0.5f);

            DrawFrame(submenuRow, 1.0f, open: true);
            Assert.AreEqual(2, DrawFrame(submenuRow, 1.05f), "Hovering the submenu row must open its child menu.");
            Assert.LessOrEqual(renderer.lastMenuPopupRect.xMax, Surface.x, "The submenu must clamp into the view.");

            var child = renderer.lastMenuPopupRect;
            var probe = new Vector2(
                Mathf.Clamp(anchor.x + menuWidth * 0.5f, child.x + 10f, child.xMax - 10f),
                anchor.y + pad + itemHeight * 1.5f);

            Assert.Greater(probe.x, anchor.x + pad, "The probe must sit over the parent's covered row.");
            Assert.Less(probe.x, anchor.x + menuWidth - pad, "The probe must sit over the parent's covered row.");
            Assert.IsTrue(renderer.lastMenuPopupRect.Contains(probe), "The probe must sit inside the clamped submenu.");

            DrawFrame(probe, 1.1f);
            DrawFrame(probe, 1.5f);
            Assert.AreEqual(2, DrawFrame(probe, 1.55f), "Dwelling over the overlap must not retarget the covered parent row.");

            DrawFrame(probe, 1.6f, down: true, pressed: true);
            DrawFrame(probe, 1.65f, released: true);
            DrawFrame(probe, 1.7f);

            Assert.AreEqual("B", clicked, "The press over the overlap must deliver the submenu item, not the covered row.");
            Assert.IsFalse(NowContextMenu.isOpen);
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void SubmenuAtTheBottomOfAScrolledMenuOpensAndDelivers()
    {
        const int menuId = 7303;
        const int itemCount = 20;
        var anchor = new Vector2(20f, 10f);
        bool nestedClicked = false;

        var theme = ScriptableObject.CreateInstance<NowThemeAsset>();
        var renderer = ScriptableObject.CreateInstance<RecordingRenderer>();
        SetRenderer(theme, renderer);

        void DrawFrame(NowInputSnapshot snapshot, bool open = false)
        {
            NowOverlay.ForceNewFrame();
            _pointer.snapshot = snapshot;

            using (NowTheme.Scope(theme))
            using (NowInput.Begin(_pointer, Surface))
            using (_drawList.Begin(Surface))
            {
                if (open)
                    NowContextMenu.Open(menuId, anchor);

                if (NowContextMenu.Begin(menuId))
                {
                    for (int i = 0; i < itemCount; ++i)
                        NowContextMenu.Item("Filler Item");

                    if (NowContextMenu.BeginSubmenu("More"))
                    {
                        if (NowContextMenu.Item("Nested Action"))
                            nestedClicked = true;

                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.End();
                }

                NowOverlay.Flush();
            }
        }

        try
        {
            var styles = theme.controlStyles;

            DrawFrame(MenuPointer(anchor), open: true);

            var menuRect = renderer.lastMenuPopupRect;
            var insideMenu = new Vector2(menuRect.x + 20f, menuRect.y + menuRect.height * 0.5f);

            for (int i = 0; i < 10; ++i)
                DrawFrame(MenuPointer(insideMenu, scroll: new Vector2(0f, -3f)));

            Assert.IsTrue(NowContextMenu.isOpen, "Scrolling to the bottom must not close the menu.");

            var submenuRowPoint = new Vector2(
                menuRect.x + 20f,
                menuRect.yMax - styles.popupPadding - styles.contextMenuItemHeight * 0.5f);

            DrawFrame(MenuPointer(submenuRowPoint));
            DrawFrame(MenuPointer(submenuRowPoint));

            var nestedPoint = new Vector2(
                menuRect.x + menuRect.width + 14f,
                submenuRowPoint.y);

            DrawFrame(MenuPointer(nestedPoint));
            DrawFrame(MenuPointer(nestedPoint, down: true, pressed: true));
            DrawFrame(MenuPointer(nestedPoint, released: true));
            DrawFrame(MenuPointer(nestedPoint));

            Assert.IsTrue(nestedClicked, "The nested item of a submenu opened from a scrolled row must deliver its click.");
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
        }
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
            Assert.AreEqual(horizontalBefore, renderer.horizontalScrollbars, "Horizontal overflow shows only once content has re-measured inside the gutter-reduced width.");

            int horizontalSettled = renderer.horizontalScrollbars;

            DrawFrame();

            Assert.Greater(renderer.horizontalScrollbars, horizontalSettled, "The vertical scrollbar gutter should reduce width enough to reveal horizontal overflow.");
        }
        finally
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(theme);
            NowLayout.Reset();
        }
    }

    (bool vertical, bool horizontal, Vector2 maxScroll) DrawStretchContentScrollFrame(NowRect viewport, float contentHeight)
    {
        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var scroll = Now.ScrollView(viewport, "stretch-flicker").Begin();
            NowLayout.Rect(height: contentHeight, stretchWidth: true);
            var result = (scroll.verticalScrollbarVisible, scroll.horizontalScrollbarVisible, scroll.maxScrollOffset);
            scroll.Dispose();
            return result;
        }
    }

    [Test]
    public void ScrollViewVerticalOverflowDoesNotFlashHorizontalBar()
    {
        NowLayout.Reset();

        try
        {
            var viewport = new NowRect(0, 0, 200, 100);

            DrawStretchContentScrollFrame(viewport, 90f);
            var fitting = DrawStretchContentScrollFrame(viewport, 150f);

            Assert.IsFalse(fitting.vertical, "Cached content still fits on the frame it grows.");
            Assert.IsFalse(fitting.horizontal);

            var crossing = DrawStretchContentScrollFrame(viewport, 150f);

            Assert.IsTrue(crossing.vertical, "Content taller than the viewport must show the vertical bar.");
            Assert.IsFalse(crossing.horizontal, "Stretch-width content measured at the full width must not fake a horizontal bar when the vertical bar appears.");
            Assert.AreEqual(0f, crossing.maxScroll.x, "No horizontal bar means no horizontal scroll range.");

            var settled = DrawStretchContentScrollFrame(viewport, 150f);

            Assert.IsTrue(settled.vertical);
            Assert.IsFalse(settled.horizontal, "Content re-measured at the gutter-reduced width must stay horizontal-bar-free.");
        }
        finally
        {
            NowLayout.Reset();
        }
    }

    [Test]
    public void ScrollViewViewportGrowthHidesVerticalBarImmediately()
    {
        NowLayout.Reset();

        try
        {
            var small = new NowRect(0, 0, 200, 100);
            var large = new NowRect(0, 0, 200, 300);

            DrawStretchContentScrollFrame(small, 150f);
            var overflowing = DrawStretchContentScrollFrame(small, 150f);

            Assert.IsTrue(overflowing.vertical, "Content taller than the small viewport must show the vertical bar.");

            var grown = DrawStretchContentScrollFrame(large, 150f);

            Assert.IsFalse(grown.vertical, "Content fitting the grown viewport must hide the vertical bar without waiting for a re-measure.");
            Assert.AreEqual(Vector2.zero, grown.maxScroll, "Fitting content has no scroll range.");
        }
        finally
        {
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

    NowInputSnapshot ButtonSnapshot(
        Vector2 pointer,
        NowPointerButtons down,
        NowPointerButtons pressed,
        NowPointerButtons released,
        float time)
    {
        return new NowInputSnapshot(
            true, pointer, pointer, Vector2.zero,
            down, pressed, released,
            Vector2.zero, Vector2.zero,
            false, false, false, false, false, false, 1, time);
    }

    void DrawDragScrollFrame(Vector2 pointer, bool down, bool pressed, bool released, float time)
    {
        _pointer.snapshot = ButtonSnapshot(
            pointer,
            NowInputSnapshot.ToButtonMask(down, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(pressed, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(released, NowPointerButton.Primary),
            time);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "drag-scroll").Begin())
        {
            NowLayout.Rect(180f, 400f);
            var content = NowInput.Interact("drag-content", new NowRect(0, 0, 200, 400));

            if (content.dragging)
                NowScrollView.RequestDragScroll();
        }
    }

    [Test]
    public void ScrollViewDragScrollsWhenPointerNearsViewportEdge()
    {
        NowLayout.Reset();

        try
        {
            DrawDragScrollFrame(new Vector2(100f, 50f), false, false, false, 0.00f);
            DrawDragScrollFrame(new Vector2(100f, 50f), false, false, false, 0.05f);
            DrawDragScrollFrame(new Vector2(100f, 50f), true, true, false, 0.10f);
            DrawDragScrollFrame(new Vector2(100f, 92f), true, false, false, 0.15f);
            DrawDragScrollFrame(new Vector2(100f, 92f), true, false, false, 0.20f);

            int scrollId;

            using (NowInput.Begin(_pointer, Surface))
                scrollId = NowControls.GetControlId("drag-scroll");

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
            float afterHold = scroll.y;
            Assert.Greater(afterHold, 0f, "Dragging near the bottom edge should auto-scroll down.");

            DrawDragScrollFrame(new Vector2(100f, 92f), true, false, false, 0.25f);
            Assert.Greater(scroll.y, afterHold, "Auto-scroll should continue while the drag holds at the edge.");

            DrawDragScrollFrame(new Vector2(100f, 92f), false, false, true, 0.30f);
            float afterRelease = scroll.y;
            DrawDragScrollFrame(new Vector2(100f, 92f), false, false, false, 0.35f);
            Assert.AreEqual(afterRelease, scroll.y, 0.001f, "Auto-scroll must stop when the drag ends.");
        }
        finally
        {
            NowLayout.Reset();
        }
    }

    [Test]
    public void ScrollViewDragScrollIgnoresPointerAwayFromEdges()
    {
        NowLayout.Reset();

        try
        {
            DrawDragScrollFrame(new Vector2(100f, 30f), false, false, false, 0.00f);
            DrawDragScrollFrame(new Vector2(100f, 30f), false, false, false, 0.05f);
            DrawDragScrollFrame(new Vector2(100f, 30f), true, true, false, 0.10f);
            DrawDragScrollFrame(new Vector2(100f, 60f), true, false, false, 0.15f);
            DrawDragScrollFrame(new Vector2(100f, 60f), true, false, false, 0.20f);

            int scrollId;

            using (NowInput.Begin(_pointer, Surface))
                scrollId = NowControls.GetControlId("drag-scroll");

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
            Assert.AreEqual(0f, scroll.y, 0.001f, "Dragging in the middle of the viewport should not auto-scroll.");
        }
        finally
        {
            NowLayout.Reset();
        }
    }

    void DrawPanScrollFrame(Vector2 pointer, bool middleDown, bool middlePressed, bool middleReleased, bool primaryPressed, float time)
    {
        _pointer.snapshot = ButtonSnapshot(
            pointer,
            NowInputSnapshot.ToButtonMask(middleDown, NowPointerButton.Middle),
            NowInputSnapshot.ToButtonMask(middlePressed, NowPointerButton.Middle) |
                NowInputSnapshot.ToButtonMask(primaryPressed, NowPointerButton.Primary),
            NowInputSnapshot.ToButtonMask(middleReleased, NowPointerButton.Middle),
            time);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(new NowRect(0, 0, 200, 100), "pan-scroll").Begin())
        {
            NowLayout.Rect(180f, 400f);
        }
    }

    [Test]
    public void ScrollViewMiddleClickPansUntilNextPress()
    {
        NowLayout.Reset();

        try
        {
            DrawPanScrollFrame(new Vector2(100f, 50f), false, false, false, false, 0.00f);
            DrawPanScrollFrame(new Vector2(100f, 50f), false, false, false, false, 0.05f);
            DrawPanScrollFrame(new Vector2(100f, 50f), true, true, false, false, 0.10f);
            DrawPanScrollFrame(new Vector2(100f, 50f), false, false, true, false, 0.15f);
            DrawPanScrollFrame(new Vector2(100f, 90f), false, false, false, false, 0.20f);
            DrawPanScrollFrame(new Vector2(100f, 90f), false, false, false, false, 0.25f);

            int scrollId;

            using (NowInput.Begin(_pointer, Surface))
                scrollId = NowControls.GetControlId("pan-scroll");

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
            float whilePanning = scroll.y;
            Assert.Greater(whilePanning, 0f, "A middle click should keep panning after release.");

            DrawPanScrollFrame(new Vector2(100f, 90f), false, false, false, true, 0.30f);
            float afterStop = scroll.y;
            DrawPanScrollFrame(new Vector2(100f, 90f), false, false, false, false, 0.35f);
            Assert.AreEqual(afterStop, scroll.y, 0.001f, "Any button press must end sticky pan mode.");
        }
        finally
        {
            NowLayout.Reset();
        }
    }

    [Test]
    public void ScrollViewMiddleDragPansAndStopsOnRelease()
    {
        NowLayout.Reset();

        try
        {
            DrawPanScrollFrame(new Vector2(100f, 50f), false, false, false, false, 0.00f);
            DrawPanScrollFrame(new Vector2(100f, 50f), false, false, false, false, 0.05f);
            DrawPanScrollFrame(new Vector2(100f, 50f), true, true, false, false, 0.10f);
            DrawPanScrollFrame(new Vector2(100f, 90f), true, false, false, false, 0.15f);
            DrawPanScrollFrame(new Vector2(100f, 90f), true, false, false, false, 0.20f);

            int scrollId;

            using (NowInput.Begin(_pointer, Surface))
                scrollId = NowControls.GetControlId("pan-scroll");

            ref Vector2 scroll = ref NowControlState.Get<Vector2>(scrollId);
            float whileHeld = scroll.y;
            Assert.Greater(whileHeld, 0f, "Holding middle away from the anchor should pan the content.");

            DrawPanScrollFrame(new Vector2(100f, 90f), false, false, true, false, 0.25f);
            float afterRelease = scroll.y;
            DrawPanScrollFrame(new Vector2(100f, 90f), false, false, false, false, 0.30f);
            Assert.AreEqual(afterRelease, scroll.y, 0.001f, "Releasing after a middle drag must end the pan.");
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
