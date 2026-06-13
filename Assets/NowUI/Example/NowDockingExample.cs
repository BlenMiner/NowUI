using UnityEngine;
using NowUI;
using NowUI.Docking;

[AddComponentMenu("NowUI/Examples/Now Docking Example")]
public sealed class NowDockingExample : MonoBehaviour
{
    [SerializeField] NowFontAsset _font;

    readonly NowDockSpace _dock = new NowDockSpace();

    bool _startupLayoutApplied;
    bool _showGrid = true;
    float _exposure = 0.65f;
    int _selectedObject = 1;

    static readonly string[] Objects =
    {
        "Main Camera",
        "Directional Light",
        "Docking Canvas",
        "Player Rig"
    };

    void Awake()
    {
        if (_font != null)
            Now.defaultFont = _font;
    }

    void OnPostRender()
    {
        Now.StartUI();

        using (NowInput.Begin(new Vector2(Screen.width, Screen.height)))
        {
            SubmitWindows();

            if (!_startupLayoutApplied)
            {
                _startupLayoutApplied = true;
                _dock.Dock("Inspector", "Scene", NowDockSide.Right);
                _dock.Dock("Hierarchy", "Scene", NowDockSide.Left);
            }

            NowDock.Space(_dock, new NowRect(24f, 24f, Screen.width - 48f, Screen.height - 48f), "main-dock")
                .SetMinPaneSize(140f)
                .Draw();
        }

        Now.FlushUI();
    }

    void SubmitWindows()
    {
        _dock.Window("Scene", DrawScene, id: "Scene");
        _dock.Window("Hierarchy", DrawHierarchy, id: "Hierarchy");
        _dock.Window("Inspector", DrawInspector, id: "Inspector");
        _dock.Window("Console", DrawConsole, id: "Console");
    }

    void DrawScene(NowRect rect)
    {
        Now.Rectangle(rect)
            .SetColor(new Color(0.08f, 0.09f, 0.11f, 1f))
            .SetRadius(3f)
            .Draw();

        if (_showGrid)
        {
            for (float x = rect.x + 20f; x < rect.xMax; x += 28f)
                Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(new Color(1f, 1f, 1f, 0.06f)).Draw();

            for (float y = rect.y + 20f; y < rect.yMax; y += 28f)
                Now.Rectangle(new NowRect(rect.x, y, rect.width, 1f)).SetColor(new Color(1f, 1f, 1f, 0.06f)).Draw();
        }

        using (NowLayout.Area(rect.Inset(18f), spacing: 8f))
        {
            NowLayout.Label("Scene View").SetFontSize(22f).Draw();
            NowLayout.Label("Drag tabs to split, merge, reorder, or float outside the dockspace.").SetFontSize(13f).Draw();
            NowLayout.FlexibleSpace();

            using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
            {
                NowLayout.Label("Exposure").SetWidth(72f).Draw();
                NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _exposure);
            }
        }
    }

    void DrawHierarchy(NowRect rect)
    {
        using (NowLayout.Area(rect, spacing: 6f))
        {
            NowLayout.Label("Objects").SetFontSize(17f).Draw();

            for (int i = 0; i < Objects.Length; ++i)
            {
                bool selected = i == _selectedObject;

                if (NowLayout.Radio(Objects[i], selected).Draw())
                    _selectedObject = i;
            }
        }
    }

    void DrawInspector(NowRect rect)
    {
        using (NowLayout.Area(rect, spacing: 8f))
        {
            NowLayout.Label("Inspector").SetFontSize(17f).Draw();
            NowLayout.Label(Objects[_selectedObject]).SetFontSize(14f).Draw();
            NowLayout.Checkbox("Show grid").Draw(ref _showGrid);

            using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
            {
                NowLayout.Label("Exposure").SetWidth(72f).Draw();
                NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _exposure);
            }
        }
    }

    void DrawConsole(NowRect rect)
    {
        using (NowLayout.Area(rect, spacing: 6f))
        {
            NowLayout.Label("Console").SetFontSize(17f).Draw();
            NowLayout.Label("Docking initialized").SetFontSize(13f).Draw();
            NowLayout.Label("No warnings").SetFontSize(13f).Draw();
        }
    }
}
