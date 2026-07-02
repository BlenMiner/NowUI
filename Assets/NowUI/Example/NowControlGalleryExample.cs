using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NowUI;

/// <summary>
/// Every built-in control on one screen, in light or dark. Attach to a camera,
/// assign a font and the Default theme, and enter play mode. The header switch
/// flips <see cref="NowTheme.preferDark"/>, exercising the counterpart link.
/// </summary>
public class NowControlGalleryExample : MonoBehaviour
{
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowThemeAsset _theme;

    static readonly string[] _tabPages = { "Inputs", "Pickers", "Data", "Menus", "Inspector" };
    static readonly string[] _qualityOptions = { "Low", "Medium", "High", "Ultra" };
    static readonly string[] _audioChannels = { "Music", "Effects", "Voice", "Ambient" };
    static readonly string[] _countries =
    {
        "Argentina", "Australia", "Austria", "Belgium", "Brazil", "Canada",
        "Denmark", "Finland", "France", "Germany", "Iceland", "Japan",
        "Netherlands", "Norway", "Portugal", "Sweden", "Switzerland"
    };

    readonly NowTreeViewState _treeState = new NowTreeViewState();

    bool _darkMode;
    int _page;
    bool _notifications = true;
    bool _autoSave;
    bool _checkbox = true;
    int _quality = 2;
    float _volume = 0.68f;
    float _spacingValue = 4f;
    int _retries = 3;
    string _playerName = "Player One";
    string _notes = "Multi-line notes.\nSecond line.";
    int _dropdownIndex = 1;
    int _comboIndex = 8;
    float _progress = 0.4f;
    bool _chipSelected = true;
    float _splitRatio = 0.35f;
    DateTime _dueDate = new DateTime(2026, 7, 2);
    TimeSpan _alarm = new TimeSpan(7, 30, 0);
    Key _jumpKey = Key.Space;
    Key _sprintKey = Key.LeftShift;
    int _channelMask = 0b0101;
    LayerMask _cameraLayers = ~0;
    bool _metadataExpanded = true;

    void Awake()
    {
        Now.defaultFont = _font;
    }

    void OnPostRender()
    {
        NowTheme.preferDark = _darkMode;

        using (Now.StartUI(NowScreen.recommendedUIScale))
        using (_theme != null ? NowTheme.Scope(_theme) : default)
        {
            DrawGallery();
        }
    }

    void DrawGallery()
    {
        var theme = NowTheme.themeAsset;
        var screen = NowScreen.safeArea;
        float scale = Now.uiScale;

        Now.Rectangle(new NowRect(0f, 0f, Screen.width / scale, Screen.height / scale))
            .SetColor(theme.GetColor(NowColorToken.Background))
            .SetRadius(0f)
            .Draw();

        using (NowLayout.Area(screen))
        using (NowLayout.Vertical(padding: 20f, spacing: 12f))
        {
            DrawHeader();

            using (var view = NowLayout.TabView(_tabPages).SetStretchWidth().SetStretchHeight().Begin(ref _page))
            {
                using (NowLayout.Vertical(padding: 16f, spacing: 12f))
                {
                    switch (view.selected)
                    {
                        case 0:
                            DrawInputsPage();
                            break;
                        case 1:
                            DrawPickersPage();
                            break;
                        case 2:
                            DrawDataPage();
                            break;
                        case 3:
                            DrawMenusPage();
                            break;
                        default:
                            DrawInspectorPage();
                            break;
                    }
                }
            }
        }
    }

    void DrawHeader()
    {
        using (NowLayout.Horizontal(spacing: 10f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Heading), "Control Gallery").Draw();
            NowLayout.Badge("v0.2").SetStyle(NowRectangleStyle.AccentSoft).Draw();
            NowLayout.Rect(stretchWidth: true);
            NowLayout.Switch("Dark").Draw(ref _darkMode);
        }

        NowLayout.ProgressBar(_progress).SetStretchWidth().Draw();
        NowLayout.ProgressBar().SetIndeterminate().SetTime(Time.time).SetStretchWidth().Draw();
    }

    void DrawInputsPage()
    {
        using (NowLayout.Horizontal(spacing: 8f))
        {
            if (NowLayout.Button("Primary").Draw())
                _progress = Mathf.Repeat(_progress + 0.1f, 1f);

            NowLayout.Button("Elevated").SetStyle(NowRectangleStyle.Elevated).Draw();
            NowLayout.Button("Outline").SetStyle(NowRectangleStyle.Outline).Draw();
            NowLayout.Button("Ghost").SetStyle(NowRectangleStyle.Ghost).Draw();
            NowLayout.Button("Soft").SetStyle(NowRectangleStyle.AccentSoft).Draw();
            NowLayout.Button("Danger").SetStyle(NowRectangleStyle.Danger).Draw();

            using (var reset = NowLayout.Button().SetAlignItems(NowLayoutAlign.Center).Begin())
            {
                if (reset.clicked)
                    _progress = 0f;

                NowLayout.Label("Reset").Draw();
                NowTooltip.For(reset, "Set the header progress back to zero");
            }
        }

        NowLayout.Checkbox("Enable checkbox").Draw(ref _checkbox);
        NowLayout.Switch("Notifications").Draw(ref _notifications);
        NowLayout.Switch("Auto-save").Draw(ref _autoSave);

        using (NowLayout.Horizontal(spacing: 8f))
        {
            for (int i = 0; i < _qualityOptions.Length; ++i)
            {
                using (NowControls.IdScope(i))
                {
                    if (NowLayout.Radio(_qualityOptions[i], _quality == i).Draw())
                        _quality = i;
                }
            }
        }

        NowLayout.TabBar(_qualityOptions).SetStretchTabs().SetWidth(360f).Draw(ref _quality);

        NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _volume);
        NowLayout.TextField().SetPlaceholder("Name...").SetStretchWidth().Draw(ref _playerName);
        NowLayout.FloatField().SetRange(0f, 32f).SetSpinner(0.5f).SetWidth(160f).Draw(ref _spacingValue);
        NowLayout.IntField().SetRange(0, 10).SetSpinner().SetWidth(160f).Draw(ref _retries);
        NowLayout.TextArea().SetPlaceholder("Notes...").SetLines(2, 4).SetStretchWidth().Draw(ref _notes);

        using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Body), "Jump").Draw();
            NowLayout.KeyBindingField().SetWidth(120f).Draw(ref _jumpKey);
            NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Body), "Sprint").Draw();
            NowLayout.KeyBindingField().SetWidth(120f).Draw(ref _sprintKey);
        }
    }

    void DrawPickersPage()
    {
        NowLayout.Dropdown(_qualityOptions).SetWidth(220f).Draw(ref _dropdownIndex);
        NowLayout.ComboBox(_countries).SetWidth(220f).Draw(ref _comboIndex);
        NowLayout.MaskField(_audioChannels).SetWidth(220f).Draw(ref _channelMask);
        NowLayout.LayerMaskField().SetWidth(220f).Draw(ref _cameraLayers);
        NowLayout.DatePicker().SetWidth(220f).SetToday(DateTime.Today).Draw(ref _dueDate);
        NowLayout.TimePicker().SetWidth(220f).Set24Hour(false).Draw(ref _alarm);

        using (NowLayout.Horizontal(spacing: 8f))
        {
            if (NowLayout.Chip("Filter: Active").SetSelected(_chipSelected).Draw())
                _chipSelected = !_chipSelected;

            NowLayout.Chip("Removable").SetRemovable().Draw(out _);
            NowLayout.Badge("12").SetStyle(NowRectangleStyle.Danger).Draw();
            NowLayout.Badge("OK").SetStyle(NowRectangleStyle.Accent).Draw();
        }
    }

    string _menuLabResult = "Right-click the playground, or use the buttons.";
    int _menuLabDropdown;

    /// <summary>
    /// Menu edge-case lab: menus taller than the screen (clamp + scroll),
    /// long submenus, deep nesting, a menu opened near the screen edge, and a
    /// popup-over-text leak check. Things to verify: every option reachable
    /// via wheel, keyboard, or the top/bottom hover strips; arrows move the
    /// highlight, Enter activates, right/left dive into and out of submenus;
    /// submenus survive diagonal pointer paths; a submenu clamped over its
    /// parent (right-click near the right screen edge) owns the pointer where
    /// they overlap; scrolling over a menu never
    /// closes it, scrolling elsewhere does; right-clicking over the open
    /// dropdown popup must NOT open the selectable text's context menu, and a
    /// right-click outside must close the popup.
    /// </summary>
    void DrawMenusPage()
    {
        NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Muted), _menuLabResult).Draw();

        var playground = NowLayout.Rect(stretchWidth: true, height: 120f);
        NowTheme.themeAsset.Rectangle(playground, NowRectangleStyle.Muted).Draw();
        NowTheme.themeAsset.Text(playground.Inset(16f, 50f, 16f, 16f), NowTextStyle.Muted)
            .Draw("Right-click playground (60-item menu + submenus)");

        var playgroundInteraction = NowInput.Interact(playground, NowPointerButton.Secondary);

        if (playgroundInteraction.clicked)
            NowContextMenu.Open(NowInput.GetId("gallery-menu-lab"), playgroundInteraction.pointerPosition);

        if (NowContextMenu.Begin(NowInput.GetId("gallery-menu-lab")))
        {
            NowContextMenu.Label("Menu Lab");
            NowContextMenu.Separator();

            if (NowContextMenu.BeginSubmenu("Long Submenu (80)"))
            {
                for (int i = 0; i < 80; ++i)
                {
                    if (NowContextMenu.Item($"Deep Cut {i + 1}"))
                        _menuLabResult = $"Picked: Deep Cut {i + 1}";
                }

                NowContextMenu.EndSubmenu();
            }

            if (NowContextMenu.BeginSubmenu("Nesting"))
            {
                if (NowContextMenu.BeginSubmenu("Deeper"))
                {
                    if (NowContextMenu.BeginSubmenu("Deeper Still"))
                    {
                        if (NowContextMenu.Item("The Bottom"))
                            _menuLabResult = "Picked: The Bottom";

                        NowContextMenu.EndSubmenu();
                    }

                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.EndSubmenu();
            }

            NowContextMenu.Separator();

            for (int i = 0; i < 60; ++i)
            {
                if (NowContextMenu.Item($"Option {i + 1}"))
                    _menuLabResult = $"Picked: Option {i + 1}";
            }

            NowContextMenu.End();
        }

        using (NowLayout.Horizontal(spacing: 8f))
        {
            var cornerButton = NowLayout.Button("Open at screen corner").Draw();

            if (cornerButton)
                NowContextMenu.Open(NowInput.GetId("gallery-corner-menu"), new Vector2(Screen.width / Now.uiScale - 30f, Screen.height / Now.uiScale - 30f));

            if (NowLayout.Button("Open tall menu here").Draw())
                NowContextMenu.Open(NowInput.GetId("gallery-tall-menu"), NowInput.current.pointerPosition);
        }

        if (NowContextMenu.Begin(NowInput.GetId("gallery-corner-menu")))
        {
            for (int i = 0; i < 25; ++i)
            {
                if (NowContextMenu.Item($"Corner Case {i + 1}"))
                    _menuLabResult = $"Picked: Corner Case {i + 1}";
            }

            NowContextMenu.End();
        }

        if (NowContextMenu.Begin(NowInput.GetId("gallery-tall-menu")))
        {
            for (int i = 0; i < 100; ++i)
            {
                if (NowContextMenu.Item($"Tall Option {i + 1}"))
                    _menuLabResult = $"Picked: Tall Option {i + 1}";
            }

            NowContextMenu.End();
        }

        NowLayout.Dropdown(_qualityOptions).SetWidth(160f).Draw(ref _menuLabDropdown);
        NowLayout.RichText(
                "Leak check: open the dropdown above, then right-click over its popup — this selectable text sits " +
                "beneath it and must not answer; a right-click beside the popup closes it and opens this text's menu.")
            .SetSelectable()
            .SetStretchWidth()
            .Draw();
    }

    void DrawDataPage()
    {
        using (var split = NowLayout.SplitView().Begin(ref _splitRatio))
        {
            using (split.BeginFirst())
            using (NowLayout.Vertical(padding: 8f, spacing: 4f))
            using (var tree = NowLayout.TreeView(_treeState).Begin())
            {
                if (tree.BeginNode("Assets"))
                {
                    if (tree.BeginNode("Textures"))
                    {
                        tree.Node("grass.png");
                        tree.Node("rock.png");
                        tree.EndNode();
                    }

                    tree.Node("Readme.md");
                    tree.EndNode();
                }

                if (tree.BeginNode("Scenes"))
                {
                    tree.Node("Main.unity");
                    tree.Node("Menu.unity");
                    tree.EndNode();
                }
            }

            using (split.BeginSecond())
            using (NowLayout.Vertical(padding: 12f, spacing: 8f))
            {
                NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Subheading), "Details").Draw();
                NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Muted), "Select a row on the left; drag the divider to resize.").Draw();

                NowLayout.Foldout("Metadata").Draw(ref _metadataExpanded);

                if (_metadataExpanded)
                {
                    NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Muted), "Type: Folder").Draw();
                    NowLayout.Label(NowTheme.themeAsset.ResolveText(NowTextStyle.Muted), "Items: 5").Draw();
                }
            }
        }
    }

    enum GalleryDifficulty
    {
        Easy,
        Normal,
        Hard,
        Nightmare
    }

    [Flags]
    enum GallerySpawnAreas
    {
        None = 0,
        Ground = 1 << 0,
        Air = 1 << 1,
        Water = 1 << 2,
        Underground = 1 << 3
    }

    [Serializable]
    class GalleryGraphicsSection
    {
        [Range(0.5f, 2f)] public float renderScale = 1f;
        public bool vsync = true;
        public AnimationCurve lodCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public Gradient skyTint = new Gradient();
    }

    /// <summary>
    /// Exercises every inspector row kind: attributes, enums and flags, vectors,
    /// quaternion-as-euler, bounds, nested foldouts, and resizable lists.
    /// </summary>
    [Serializable]
    class GalleryInspectorTarget
    {
        [Header("Profile")]
        public string playerName = "Player One";
        [Range(1, 99)] public int level = 12;
        [Min(0f)] public float health = 87.5f;
        public GalleryDifficulty difficulty = GalleryDifficulty.Normal;
        public GallerySpawnAreas spawnAreas = GallerySpawnAreas.Ground | GallerySpawnAreas.Air;
        public LayerMask hitLayers = ~0;

        [Header("Transform")]
        public Vector3 offset = new Vector3(0f, 1f, 0f);
        public Quaternion facing = Quaternion.identity;
        public Rect viewport = new Rect(0f, 0f, 1f, 1f);
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        [Header("Look")]
        public Color tint = new Color(0.4f, 0.7f, 1f);
        public GalleryGraphicsSection graphics = new GalleryGraphicsSection();

        [Header("Notes")]
        public System.Collections.Generic.List<string> tags = new System.Collections.Generic.List<string> { "alpha", "beta" };
        [TextArea(2, 4)] public string bio = "Reflection-driven inspector: NowLayout.Inspector().Draw(ref target).";
    }

    GalleryInspectorTarget _inspectorTarget = new GalleryInspectorTarget();

    void DrawInspectorPage()
    {
        NowLayout.Label(
                NowTheme.themeAsset.ResolveText(NowTextStyle.Muted),
                "NowLayout.Inspector().Draw(ref target) renders Unity-style rows for any serializable type.")
            .Draw();

        using (NowLayout.ScrollView().Begin())
        using (NowLayout.Vertical(padding: new Vector4(0f, 0f, 12f, 0f), spacing: 4f, stretchWidth: true))
        {
            NowLayout.Inspector().SetToday(DateTime.Today).Draw(ref _inspectorTarget);
        }
    }
}
