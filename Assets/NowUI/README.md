# NowUI

NowUI is an immediate-mode UI renderer for Unity. It provides explicit and
measured layout APIs, controls, MSDF text, gradients, themes, effects, vector
animation, and hosts for UGUI, UI Toolkit, render pipelines, RenderTextures,
IMGUI, and world-space meshes.

## Start here

- AI coding agents: start with [AI Guide](Documentation~/AI_GUIDE.md).
  For package contributions, also read [AGENTS.md](AGENTS.md).
- Everyone else: open the [documentation index](Documentation~/README.md) and
  import the Quick Start sample from Package Manager.
- For exact API questions, consult the relevant guide first, then the XML
  comments on the installed source under `Runtime`, `Editor`, or `Extensions`.

The files in this package are version-matched to the installed code. Prefer
them over documentation from another branch or release.

## Pick the API in two decisions

First choose the host for the target surface:

| Surface | Explicit placement | Measured layout |
| --- | --- | --- |
| UGUI Canvas | `NowGraphic` | `NowLayoutGraphic` |
| UI Toolkit | `NowVisualElement` | `NowLayoutVisualElement` |
| URP or HDRP | `NowPipelineGraphic` | `NowPipelineLayoutGraphic` |
| World-space mesh | `NowWorldGraphic` | `NowWorldLayoutGraphic` |
| Manual/Built-in callback | `Now.StartUI(...)` | `Now.StartUI(...)` + `NowLayout.RunMeasured(...)` |

Then choose placement and the corresponding type from that row:

- Use `Now` when you already have a `NowRect` and want exact placement.
- Use `NowLayout` when NowUI should arrange rows, columns, spacing, growth, and
  alignment. `Row`/`Horizontal` and `Column`/`Vertical` are identical fluent
  naming pairs.
- Use `NowLayout.ReserveRect(...)` to reserve layout space for an explicit
  `Now` primitive.

The UGUI row is available when Unity resolves `com.unity.ugui`; the UI Toolkit
row is available when it resolves `com.unity.modules.uielements`. NowUI detects
either package whether it is a direct or transitive dependency, so consumers do
not need to add scripting defines. Projects that use one of these optional hosts
only need to ensure the corresponding package is resolved, adding it to their
own manifest when no other dependency supplies it. NowUI no longer installs
either package itself.

`com.unity.inputsystem` is optional and is detected the same way. The default
provider prefers it when installed and enabled, then falls back to the legacy
Input Manager when that backend is enabled. The legacy path covers mouse,
touch, keyboard navigation, text, and IME; reliable default gamepad navigation
requires the Input System because legacy axes and buttons are project-defined.
`KeyBindingField`, `NowKeyInput`, and `NowKeyNames` are compiled only when the
Input System package resolves because their public API uses
`UnityEngine.InputSystem.Key`.

See [Render Pipelines](Documentation~/RenderPipelines.md),
[World Space](Documentation~/WorldSpace.md), and
[Layout](Documentation~/Layout.md) before implementing a new host.

## Minimal layout example

This example uses the optional UGUI integration and requires
`com.unity.ugui` to be resolved.

```csharp
using NowUI;

public sealed class SettingsPanel : NowLayoutGraphic
{
    bool _shadows;

    protected override void DrawNowUI(NowRect view)
    {
        using (NowLayout.Column(view).Padding(16).Gap(8).Begin())
        {
            NowLayout.Label("Settings").SetFontSize(24).Draw();
            NowLayout.Checkbox("Enable shadows").Draw(ref _shadows);

            if (NowLayout.Button("Save").Draw())
                Save();
        }
    }

    void Save()
    {
    }
}
```

Dedicated hosts own the frame and, for layout hosts, the measure/draw cycle.
Do not call `Now.StartUI` or `NowLayout.RunMeasured` inside their
`DrawNowUI(...)` methods.

## Agent integration

The [AI guide](Documentation~/AI_GUIDE.md) routes usage tasks to the relevant
feature docs. [AGENTS.md](AGENTS.md) adds package contribution rules. To make the
guide discoverable from a consuming project, choose either integration:

- **Skill:** In Unity, choose **Tools > NowUI > AI > Install Agent Skill**.
  This copies [the packaged skill](AI~/skills/nowui/SKILL.md) to the project's
  `.agents/skills/nowui`. This is a supported [Codex skill location](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills);
  open Codex at the Unity project root or below. If the skill does not appear,
  restart Codex. Other agents may use different skill locations.
- **Project instructions:** Choose **Tools > NowUI > AI > Copy Project AGENTS.md Snippet**
  and paste the block into the consuming project's root `AGENTS.md`. The
  [snippet](AI~/AGENTS.snippet.md) selects NowUI for new custom UI while
  preserving existing implementations and explicit framework choices. Edit
  that preference if needed, and replace an existing marked block rather than
  appending duplicates.

Neither action runs on package import. Rerun the skill installer after package
updates to refresh the router; it updates known unmodified copies and leaves
customized or unrecognized installations untouched. Merge those copies
manually. The skill always reads API guidance from the active package, so it
does not carry a frozen copy of the API docs. You can also copy the entire
`AI~/skills/nowui` folder manually; the installer will not overwrite a differing
manual copy without its install receipt.

## Important rules

- Cached packages are read-only dependencies. Put consumer code and assets
  under the project's `Assets` directory.
- Builders are inert until consumed with `.Draw()` or `.Begin()`; use returned
  scopes in `using` statements.
- Use authored model keys with `SetId`, `ControlScope`, or `KeyedItem` for
  dynamic, conditional, or reorderable collections. Keep opaque
  `NowResolvedId` values runtime-only; do not hash model strings into integers.
  Integer zero is a valid authored `NowId`; `NowControls.SiteId(...)` returns a
  typed `NowCallSiteId` fallback, not an authored or resolved identity.
- Use `NowContextAction` for secondary-pointer/action-button menus, exclude
  child controls with `NowInteractionRegion`, and give every item/submenu a
  stable `id:`. Positional menu entries and raw resolved-looking integer IDs
  are compile-time errors.
- An anonymous overlay callback's `int state` is payload only. Pass a separate
  `NowResolvedId` to a named `NowOverlay.Defer` overload when the overlay needs
  source identity; the deferred callback receives its queued input/host/id
  context.
- Treat `NOWUI001` and `NOWUI002` analyzer diagnostics as correctness issues.
- Allocation-free claims apply after representative warmup, not necessarily on
  first use.

The complete feature list and topic routes are in the
[documentation index](Documentation~/README.md).

## License

NowUI is available under the [MIT License](LICENSE.md). Bundled third-party
components remain under their respective licenses; see
[THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).
