# IMGUI

`NowUIGUI` renders NowUI inside IMGUI. It can be called from any existing
`OnGUI` method in runtime scripts, custom inspectors, property drawers, editor
windows, or small debugging panels. Callers do not inherit from a NowUI-specific
base class.

The bridge renders NowUI into an internally cached `RenderTexture`, then draws
that texture into the requested IMGUI rect. Rendering only happens during
`EventType.Repaint`.

## Runtime OnGUI

```csharp
using UnityEngine;

public sealed class RuntimeOnGUIExample : MonoBehaviour
{
    [SerializeField] NowFont font;

    void OnGUI()
    {
        Rect rect = GUILayoutUtility.GetRect(320, 120);

        using (var ui = NowUIGUI.Auto(rect))
        {
            NowUI.Rectangle(new Vector4(0, 0, ui.width, ui.height))
                .SetColor(new Color(0.08f, 0.1f, 0.14f, 1f))
                .SetRadius(10)
                .Draw();

            NowUI.Text(new Vector4(18, 16, ui.width - 36, 28), font)
                .SetFontSize(18)
                .SetColor(Color.white)
                .Draw("NowUI in IMGUI");
        }
    }
}
```

## Editor OnGUI

The runtime API also works in editor IMGUI. `NowUIEditorGUI` and
`NowUIEditorGUILayout` are editor-only aliases that pass editor pixel density to
the runtime renderer and dispose cached textures before assembly reload.

```csharp
using UnityEditor;
using UnityEngine;

public sealed class MyWindow : EditorWindow
{
    [SerializeField] NowFont font;

    void OnGUI()
    {
        Rect rect = GUILayoutUtility.GetRect(320, 120);

        using (var ui = NowUIEditorGUI.Auto(rect))
        {
            NowUI.Rectangle(new Vector4(0, 0, ui.width, ui.height))
                .SetColor(Color.black)
                .SetRadius(10)
                .Draw();
        }
    }
}
```

## GUILayout Helpers

Use `NowUIGUILayout` when the control should reserve layout space in runtime
IMGUI. Use `NowUIEditorGUILayout` when doing the same inside editor code.
`NowUIEditorGUI.Auto()` is a convenience shorthand for an editor layout rect
with the default preview height.

```csharp
using (var ui = NowUIEditorGUI.Auto())
{
    NowUI.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.black)
        .SetRadius(8)
        .Draw();
}
```

```csharp
using (var ui = NowUIGUILayout.Auto(96))
{
    NowUI.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.black)
        .SetRadius(8)
        .Draw();
}
```

## Clear Color

Pass a clear color when the preview should be opaque.

```csharp
using (var ui = NowUIGUI.Auto(rect, Color.white))
{
    NowUI.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.white)
        .Draw();
}
```

## Notes

- `NowUIGUIScope.rect`, `width`, and `height` use IMGUI point units.
- `NowUIEditorGUI` accounts for editor pixel density automatically.
- The cache is keyed by IMGUI control ID.
- Non-Repaint events create a no-op scope so draw calls inside the block are
  ignored instead of leaking into another NowUI target.
- Call `NowUIGUI.DisposeAll()` if a runtime host needs to eagerly release all
  cached render textures.
