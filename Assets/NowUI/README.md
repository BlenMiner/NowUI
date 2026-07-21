# NowUI

NowUI is an immediate-mode UI renderer for Unity. It provides explicit and
measured layout APIs, controls, MSDF text, themes, effects, vector animation,
and hosts for UGUI, UI Toolkit, render pipelines, RenderTextures, IMGUI, and
world-space meshes.

## Start here

- AI coding agents: read [AGENTS.md](AGENTS.md), then
  [Documentation~/AI_GUIDE.md](Documentation~/AI_GUIDE.md).
- Everyone else: open the [documentation index](Documentation~/README.md) and
  import the Quick Start sample from Package Manager.
- For exact API questions, consult the relevant guide first, then the XML
  comments on the installed source under `Runtime`, `Editor`, or `Extensions`.

The files in this package are version-matched to the installed code. Prefer
them over documentation from another branch or release.

## Pick the API in two decisions

First choose placement:

- Use `Now` when you already have a `NowRect` and want exact placement.
- Use `NowLayout` when NowUI should arrange rows, columns, spacing, growth, and
  alignment.
- Use `NowLayout.ReserveRect(...)` to reserve layout space for an explicit
  `Now` primitive.

Then choose the host:

| Surface | Explicit placement | Measured layout |
| --- | --- | --- |
| UGUI Canvas | `NowGraphic` | `NowLayoutGraphic` |
| UI Toolkit | `NowVisualElement` | `NowLayoutVisualElement` |
| URP or HDRP | `NowPipelineGraphic` | `NowPipelineLayoutGraphic` |
| World-space mesh | `NowWorldGraphic` | `NowWorldLayoutGraphic` |
| Manual/Built-in callback | `Now.StartUI(...)` | `Now.StartUI(...)` + `NowLayout.RunMeasured(...)` |

See [Render Pipelines](Documentation~/RenderPipelines.md),
[World Space](Documentation~/WorldSpace.md), and
[Layout](Documentation~/Layout.md) before implementing a new host.

## Minimal layout example

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

This package ships three complementary layers:

1. `AGENTS.md` provides concise instructions when an agent is working in the
   package tree.
2. `Documentation~/AI_GUIDE.md` is the versioned feature and practice router.
3. `AI~/skills/nowui` is an installable skill that locates the active package
   and loads the local guide on demand.

In Unity, choose **Tools > NowUI > AI > Install Agent Skill** to install the skill into
the current project's `.agents/skills/nowui` directory. The action is explicit,
will not run on package import, and protects locally modified installations.
For project-level instructions usable without a skill, choose
**Tools > NowUI > AI > Copy Project AGENTS.md Snippet** and paste the copied block into
the consuming project's root `AGENTS.md`.

## Important rules

- Cached packages are read-only dependencies. Put consumer code and assets
  under the project's `Assets` directory.
- Builders are inert until consumed with `.Draw()` or `.Begin()`; use returned
  scopes in `using` statements.
- Prefer stable non-zero integer IDs for controls in dynamic, conditional, or
  reorderable collections.
- Treat `NOWUI001` and `NOWUI002` analyzer diagnostics as correctness issues.
- Allocation-free claims apply after representative warmup, not necessarily on
  first use.

The complete feature list and topic routes are in the
[documentation index](Documentation~/README.md).
