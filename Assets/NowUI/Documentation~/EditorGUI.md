# IMGUI

`NowGUI` renders NowUI inside IMGUI. It can be called from any existing
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

        using (var ui = NowGUI.Auto(rect))
        {
            Now.Rectangle(new Vector4(0, 0, ui.width, ui.height))
                .SetColor(new Color(0.08f, 0.1f, 0.14f, 1f))
                .SetRadius(10)
                .Draw();

            Now.Text(new Vector4(18, 16, ui.width - 36, 28), font)
                .SetFontSize(18)
                .SetColor(Color.white)
                .Draw("NowUI in IMGUI");
        }
    }
}
```

## Editor OnGUI

Editor IMGUI must use `NowEditorGUI` or `NowEditorGUILayout`. Direct
`NowGUI`/`NowGUILayout` calls are runtime/legacy entry points: they do not
provide the owning `EditorWindow` identity, so they cannot isolate native
control IDs across GUIViews or route repaint requests safely. The editor
wrappers also pass editor pixel density and dispose cached textures before
assembly reload.

```csharp
using UnityEditor;
using UnityEngine;

public sealed class MyWindow : EditorWindow
{
    [SerializeField] NowFont font;

    void OnGUI()
    {
        Rect rect = GUILayoutUtility.GetRect(320, 120);

        using (var ui = NowEditorGUI.Auto(rect))
        {
            Now.Rectangle(new Vector4(0, 0, ui.width, ui.height))
                .SetColor(Color.black)
                .SetRadius(10)
                .Draw();
        }
    }
}
```

## GUILayout Helpers

Use `NowGUILayout` when the control should reserve layout space in runtime
IMGUI. Use `NowEditorGUILayout` when doing the same inside editor code.
`NowEditorGUI.Auto()` is a convenience shorthand for an editor layout rect
with the default preview height.

```csharp
using (var ui = NowEditorGUI.Auto())
{
    Now.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.black)
        .SetRadius(8)
        .Draw();
}
```

```csharp
using (var ui = NowGUILayout.Auto(96))
{
    Now.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.black)
        .SetRadius(8)
        .Draw();
}
```

## Clear Color

Pass a clear color when the preview should be opaque.

```csharp
using (var ui = NowEditorGUI.Auto(rect, Color.white))
{
    Now.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.white)
        .Draw();
}
```

## Notes

- `NowGUIScope.rect`, `width`, and `height` use IMGUI point units.
- `NowEditorGUI` accounts for editor pixel density automatically.
- Editor panel state is keyed by owning `EditorWindow` plus native control ID,
  so identical control numbers in separate windows do not share rendering,
  input, control state, or focus. This also keeps docked tabs isolated when
  Unity reuses one HostView for different windows; a non-window host falls back
  to its native GUI context.
- A live editor window retains all of its cached panels while the whole window
  is idle. On resume, one cleanup interval lets every visible sibling refresh
  even when it draws after the first panel in the same IMGUI pass. If the
  window remains active, panel IDs still absent past the cache lifetime are
  reclaimed, including their renderer, RenderTexture, focus/input state, and
  scheduled repaint. Closing the window disposes its remaining panel cache
  immediately.
- Each cached panel owns a separate focus/Tab host and native IMGUI pointer
  capture. A drag continues to receive its routed MouseDrag and MouseUp when
  the pointer leaves the panel. If Unity reports capture loss instead, the
  active interaction is cancelled, not clicked or committed; custom controls
  can inspect `NowInteraction.cancelled` and `dragCancelled`.
- Losing application or owning-window focus clears that panel's NowUI focus,
  cancels its tracked pointer state, and resets held navigation, submit, and
  cancel latches. Returning focus cannot resume a stale drag or held key.
- Non-Repaint events suppress drawing but still run control/input logic, so
  MouseDown, MouseUp, and keyboard events are handled in their native IMGUI
  pass without leaking geometry into another NowUI target.
- IMGUI event identity is `NowInput.current.inputPass`, not
  `Time.frameCount`. Unity may dispatch several keyboard, pointer, Layout, and
  Repaint passes before the frame counter advances. One-shot claims and custom
  same-pass guards must use the input-pass token.
- A primary MouseDown on empty NowUI space clears control focus at input-scope
  completion, including when IMGUI dispatches several events in one Unity
  frame.
- Focused text editors claim one-shot characters and shortcut edges for their
  input pass, so a later Layout/Repaint pass in the same `Time.frameCount`
  cannot type or invoke the same event again. Each editor panel copies native
  IMGUI KeyDown/KeyUp text semantics and, when the optional Input System-backed
  `KeyBindingField` is compiled, its raw binding key into a provider-owned
  packet before a control can mark the event used. This remains reliable when
  Unity's passive native host control filters the keyboard route to `Ignore`,
  and does not depend on a later global Input System sample. Pointer capture
  still follows Unity's filtered `GetTypeForControl` route.
- Custom keyboard consumers read `NowTextInput.current` first and call
  `NowTextInput.ClaimActivity()` only for activity they handle. Calling
  `NowTextInput.RequestTextCapture(claimActivity: false)` keeps text and IME
  delivery active without claiming the current key. The parameterless overload
  retains its legacy capture-and-claim behavior for existing custom editors.
- NowUI consumes handled native events at the ownership point: claimed KeyDown,
  captured/claimed pointer presses and their routed drag/release, empty-space
  focus-clearing presses, Tab, and owned wheel ticks are marked used so a
  sibling IMGUI control or panel cannot handle them again.
- Enter or Return submits and blurs a single-line `TextField`. `TextArea`
  continues to insert a newline, and controls that intentionally retain focus
  keep their documented behavior.
- Tab and Shift+Tab traverse the most recently registered NowUI controls during
  their native IMGUI key pass. The key event is consumed once and requests an
  editor repaint; traversal does not wait for `Time.frameCount` to advance.
- Wheel events consumed by a NowUI scrollable are marked used and explicitly
  request an editor repaint, preventing enclosing IMGUI scroll views from
  handling the same wheel tick. A modal popup owns wheel input while the
  pointer is over it, even though its content draws later, and contains an
  unhandled tick at its own scroll edge so the background does not move.
  Ordinary nested scroll views are different: when the inner view cannot move
  farther, the enclosing view may consume the tick.
- Focus, popup, pointer-footprint, and overlay-block registrations are replaced
  for each panel input pass. Repeated Layout/Repaint passes in one Unity frame
  therefore remain bounded rather than accumulating stale hit regions.
- Overlay footprint replacement is transactional. If a deferred overlay
  callback throws, provisional blocks from that failed pass are rolled back
  and the same owner's last completed footprint remains authoritative. A
  failed popup therefore creates neither an invisible new hit block nor a
  one-pass hole where its prior valid popup was registered.
- Editor repaint requests are coalesced, bounded to 60 Hz, and deferred until
  the current IMGUI dispatch finishes, avoiding nested repaint feedback in
  windows that also update live content. Both immediate requests and future
  deadlines are routed back to the provider's owning `EditorWindow`, not
  whichever window happens to be focused or under the mouse. Temporal-only
  repaint demand does not set `GUI.changed`.
- Immediate-mode controls forward their tracked animation repaint demand to the
  editor host. Merely keeping a static popup open does not request another
  repaint. A focused caret schedules only its next half-period blink boundary,
  allowing the window to idle between phase changes instead of repainting
  continuously. When no transition, repeat, scheduled phase change, or live
  content remains, the editor window fully converges to idle. Do not add an
  unconditional `EditorApplication.update => Repaint()` loop; reserve a
  bounded update loop for genuinely live data.
- Reusable custom controls still need unique scope ancestry. Wrap the complete
  body in `NowControls.ControlScope(id, file, line)`, forward caller file/line
  from public wrappers, and give reorderable instances stable explicit IDs.
- Call `NowGUI.DisposeAll()` if a runtime host needs to eagerly release all
  cached render textures.
