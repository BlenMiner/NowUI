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
input triggers a repaint. Empty primary presses clear focus; overlays should
call `NowFocus.RetainFocus()` only when dismissing or selecting them must
preserve focus-owned state. Pass theme and builder colors as authored
display/sRGB values rather than pre-converting with `.linear`. If popup
rendering or palette roles are customized, keep selected-row text visibly
distinct from its background; the built-in renderer uses `Text` over
`AccentMuted`.
<!-- nowui-agent-guidance:end -->
