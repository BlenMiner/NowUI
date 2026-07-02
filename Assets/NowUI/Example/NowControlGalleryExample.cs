using System;
using UnityEngine;
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

    static readonly string[] _tabPages = { "Inputs", "Pickers", "Data", "Menus" };
    static readonly string[] _qualityOptions = { "Low", "Medium", "High", "Ultra" };
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
                        default:
                            DrawMenusPage();
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

        NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _volume);
        NowLayout.TextField().SetPlaceholder("Name...").SetStretchWidth().Draw(ref _playerName);
        NowLayout.FloatField().SetRange(0f, 32f).SetSpinner(0.5f).SetWidth(160f).Draw(ref _spacingValue);
        NowLayout.IntField().SetRange(0, 10).SetSpinner().SetWidth(160f).Draw(ref _retries);
        NowLayout.TextArea().SetPlaceholder("Notes...").SetLines(2, 4).SetStretchWidth().Draw(ref _notes);
    }

    void DrawPickersPage()
    {
        NowLayout.Dropdown(_qualityOptions).SetWidth(220f).Draw(ref _dropdownIndex);
        NowLayout.ComboBox(_countries).SetWidth(220f).Draw(ref _comboIndex);
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

    /// <summary>
    /// Menu edge-case lab: menus taller than the screen (clamp + scroll),
    /// long submenus, deep nesting, and a menu opened near the screen edge.
    /// Things to verify: every option reachable via wheel or the top/bottom
    /// hover strips, submenus survive diagonal pointer paths, scrolling over a
    /// menu never closes it, scrolling elsewhere does.
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
            }
        }
    }
}
