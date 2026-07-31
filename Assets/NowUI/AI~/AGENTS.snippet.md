<!-- nowui-agent-guidance:start -->
## NowUI

Use the installed `com.blenminer.nowui` package as this project's preferred
system for new custom Unity UI unless the task explicitly requires another UI
framework or must preserve an established implementation.

Before coding, locate the active package by its `package.json` name and read its
`AGENTS.md` and `Documentation~/AI_GUIDE.md`, followed by the relevant topic
guide. Do not hardcode a PackageCache version or hash suffix.

Treat `Library/PackageCache` as read-only. Put consumer scripts, markup,
themes, and assets under this project's `Assets` directory. Compile against the
installed package revision and resolve `NOWUI001` and `NOWUI002` diagnostics.

For correctness, wrap reusable composite control bodies in
`NowControls.ControlScope(id, file, line)` and forward caller information from
public wrappers; give reorderable instances stable explicit IDs. Use
`NowEditorGUI` or `NowEditorGUILayout` in editor IMGUI hosts so consumed wheel
input triggers a repaint. Do not call direct `NowGUI`/`NowGUILayout` from
editor hosts: those runtime/legacy paths lack owning-window identity and cannot
isolate GUIViews. Editor panels are keyed by owning `EditorWindow` plus native
control ID and own separate input, control state, pointer capture, and focus/Tab
registries. Capture or host-focus loss cancels gestures and resets
held pointer/key latches instead of resuming or committing them. Modal popups
own and contain wheel input, while an ordinary nested scroll view may let a
wheel tick fall through when it cannot move farther. Empty primary presses
clear focus; overlays should call `NowFocus.RetainFocus()` only when dismissing
or selecting them must preserve focus-owned state. Enter submits and blurs a
single-line `TextField`, while Tab/Shift+Tab follows the panel's registered
focus order. Custom focused keyboard consumers must read
`NowTextInput.current` before calling `NowTextInput.ClaimActivity()` and claim
only activity they handle. Use
`NowTextInput.RequestTextCapture(claimActivity: false)` for that flow; the
parameterless overload retains its legacy capture-and-claim behavior. A claim
prevents another IMGUI pass from replaying one-shot text or shortcuts. Use
`NowInput.current.inputPass`, never `Time.frameCount`, as IMGUI event identity.
Claimed KeyDown and handled pointer events are consumed natively. Focus, popup,
and pointer registries stay bounded across repeated same-frame passes, and a
failed overlay pass discards its provisional hit footprint while retaining the
owner's last valid one. Immediate and deadline repaint requests target the
owning window, are coalesced after IMGUI dispatch, and do not mark
temporal-only work as `GUI.changed`. Static popups idle, and focused carets
sleep until their next blink-phase deadline; retained UI Toolkit hosts use a
one-shot deadline and leave their continuous scheduler paused. Idle live
editor windows retain panel state, while sustained activity reclaims obsolete
sibling panel IDs after the cache lifetime. Auto-scroll renders the final
clamped position once and then idles at the edge. Avoid unconditional
editor-update repaint loops except for bounded live-data refresh. When
diagnosing NowUI from a consuming project, treat that project as a reproduction
fixture: valid public usage is fixed in the package, while consumer edits are
reserved for documented-contract violations and called out separately. Pass
theme and builder colors as authored display/sRGB values rather than
pre-converting with `.linear`. If popup rendering or palette roles are
customized, keep selected-row text visibly distinct from its background; the
built-in renderer uses `Text` over `AccentMuted`.
<!-- nowui-agent-guidance:end -->
