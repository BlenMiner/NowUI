---
name: nowui
description: Build, modify, review, or debug Unity interfaces with the installed com.blenminer.nowui package, including HUDs, menus, panels, editor tools, UGUI, UI Toolkit, render-pipeline and world-space hosts, layout, controls, text, themes, effects, Markdown, markup, docking, node graphs, and other NowUI extensions. Use when the user names NowUI, project instructions select it, or existing code already uses NowUI. Do not replace a user-selected or established different UI framework merely because NowUI is installed.
---

# NowUI

Use the installed package as the version-specific source of truth. Keep this
skill focused on discovery and workflow; do not rely on copied API details.

## Resolve the active package

1. Confirm that `com.blenminer.nowui` is installed or that the current
   repository is the NowUI source checkout. Do not add or upgrade the package
   unless the user asks.
2. Locate candidate `package.json` files in this order:
   - `Packages/com.blenminer.nowui/package.json` for an embedded package.
   - `Assets/NowUI/package.json` in the NowUI source checkout.
   - `Library/PackageCache/com.blenminer.nowui@*/package.json` for a cached
     package.
3. Validate that the manifest `name` is `com.blenminer.nowui`. Do not hardcode
   a semantic version after `@`; Unity cache suffixes can be content hashes.
4. If multiple cache candidates exist, use the project lock data, generated
   project references, or Unity package information to identify the active
   one. Do not choose a candidate merely by lexical order.
5. If the package is absent, report that fact. Do not hallucinate its API or
   change `Packages/manifest.json` without authorization.

## Load local guidance

1. Read `<package-root>/AGENTS.md`.
2. Read `<package-root>/Documentation~/AI_GUIDE.md` completely.
3. Follow its host and placement routers.
4. Read only the linked feature guides relevant to the task.
5. Search the installed public source and XML comments for uncertain signatures.
6. Use packaged examples and tests to confirm behavior, not as permission to
   call internal APIs.

Prefer local, installed documentation over GitHub `main` or model memory.

## Implement safely

1. Determine whether the task is consumer work or an explicit NowUI package
   contribution.
2. For consumer work, treat PackageCache as read-only and create or modify
   files under the project's `Assets` directory.
3. Choose the host before the drawing or control API.
4. Choose `Now` for known rectangles, `NowLayout` for measured arrangement, or
   `NowLayout.ReserveRect(...)` for mixed placement.
5. Preserve host lifecycle, stable ID, draw order, ownership, and warmup rules
   from the installed guide.
6. Add required `NowUI.*` assembly references when consumer code uses an
   assembly definition.

## Verify

1. Compile the Unity project against the installed package.
2. Resolve `NOWUI001` and `NOWUI002` analyzer diagnostics.
3. Run relevant project tests or focused scene/play-mode checks.
4. Warm representative state before evaluating allocation-sensitive paths.
5. Review the diff and confirm consumer work did not modify PackageCache.
