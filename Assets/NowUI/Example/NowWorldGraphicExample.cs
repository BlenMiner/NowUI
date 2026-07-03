using UnityEngine;
using NowUI;

[AddComponentMenu("NowUI/Examples/Now World Graphic Example")]
public sealed class NowWorldGraphicExample : NowWorldGraphic
{
    [SerializeField] string _title = "Player";
    [SerializeField] string _detail = "Hover for details";
    [SerializeField] NowLottieAsset _lottie;

    AnimationCurve _animationCurve = AnimationCurve.Linear(0, 0, 1, 1);
    string _lastMenuChoice = "";

    protected override void DrawNowUI(NowRect rect)
    {
        Now.Glass(rect)
            .SetTint(new Color(0, 0, 0, 0.5f))
            .SetRadius(10f)
            .SetBlurRadius(44f)
            .Draw();

        var background = NowInput.Interact(rect, NowPointerButton.Secondary);

        if (background.clicked)
            NowContextMenu.Open(NowInput.CombineId(GetEntityId().GetHashCode(), 0x574d4c62), background.pointerPosition);

        DrawMenuLab();

        using (NowLayout.Area(rect, padding: 10, spacing: 6))
        {
            NowLayout.Label(_title).Draw();

            if (!string.IsNullOrEmpty(_lastMenuChoice))
                NowLayout.Label(_lastMenuChoice, 11f).Draw();

            NowLayout.Lottie(_lottie)
                .SetWidth(32)
                .SetTime(Time.time)
                .Draw();

            NowLayout.AnimationCurveField()
                .SetTimeRange(0f, 1f)
                .SetValueRange(0f, 1f)
                .Draw(ref _animationCurve);

            if (NowLayout.Button(_detail).Draw())
            {
                Debug.Log("Clicked", this);
            }
        }
    }

    /// <summary>
    /// Right-click anywhere on the panel: a menu far taller than the surface
    /// (clamps and scrolls), a long submenu (scrolls independently), and a
    /// deep chain — the world-space stress cases in one place.
    /// </summary>
    void DrawMenuLab()
    {
        if (!NowContextMenu.Begin(NowInput.CombineId(GetEntityId().GetHashCode(), 0x574d4c62)))
            return;

        NowContextMenu.Label("World Menu Lab");
        NowContextMenu.Separator();

        if (NowContextMenu.BeginSubmenu("Long Submenu (50)"))
        {
            for (int i = 0; i < 50; ++i)
            {
                if (NowContextMenu.Item($"Submenu Option {i + 1}"))
                    _lastMenuChoice = $"Picked: Submenu Option {i + 1}";
            }

            NowContextMenu.EndSubmenu();
        }

        if (NowContextMenu.BeginSubmenu("Deep Chain"))
        {
            if (NowContextMenu.BeginSubmenu("Level 2"))
            {
                if (NowContextMenu.BeginSubmenu("Level 3"))
                {
                    if (NowContextMenu.Item("Buried Treasure"))
                        _lastMenuChoice = "Picked: Buried Treasure";

                    NowContextMenu.EndSubmenu();
                }

                NowContextMenu.EndSubmenu();
            }

            NowContextMenu.EndSubmenu();
        }

        NowContextMenu.Separator();

        for (int i = 0; i < 40; ++i)
        {
            if (NowContextMenu.Item($"Overflow Option {i + 1}"))
                _lastMenuChoice = $"Picked: Overflow Option {i + 1}";
        }

        NowContextMenu.End();
    }
}
