# NowUI Agent Instructions

These instructions apply to work performed inside the NowUI package tree. A
consumer project can copy `AI~/AGENTS.snippet.md` into its root `AGENTS.md` or
install the packaged `nowui` skill for automatic task routing.

## Determine the operating mode

- If this package is under `Library/PackageCache`, treat it as a read-only
  dependency. Implement consumer code under the project's `Assets` directory.
- If this is the NowUI source checkout, edit package code only when the task
  asks for a package change. Preserve unrelated worktree changes.
- If the package was embedded under `Packages/com.blenminer.nowui`, do not
  assume that modifying it is desired; distinguish package work from consumer
  work first.

## Start with the installed documentation

Read `Documentation~/AI_GUIDE.md` before designing or changing NowUI usage.
Then read only the feature guides relevant to the task. Use this precedence:

1. This installed package's documentation.
2. XML documentation and public source in this installed package.
3. Packaged samples and tests.
4. Version-matched remote documentation only when local material is missing.

Do not use GitHub `main` as authority for a different installed revision and do
not infer APIs from feature names or internal design notes.

## Consumer rules

- Choose the rendering host before choosing drawing or control APIs.
- Use `Now` for known rectangles and `NowLayout` for measured arrangement.
- Never call `Now.StartUI` inside a host's `DrawNowUI`; hosts own their frame.
- Use `NowLayout.RunMeasured` only with a manual host, never a layout host.
- Finish builders with `.Draw()` or `.Begin()` and use scopes with `using`.
- Prefer stable non-zero integer IDs for dynamic or reorderable data.
- Call `MarkDirty()` when retained host state changes.
- Dispose caller-owned renderers, command buffers, model previews, and similar
  resources.
- Treat `NOWUI001` and `NOWUI002` analyzer diagnostics as correctness issues.
- Compile the consuming project and fix errors against the installed API.

## Contributor rules

- Keep public hot paths free of hidden managed allocation after documented
  warmup.
- Preserve host lifecycle, ID scoping, draw order, and ownership contracts.
- Update the package-local documentation and samples with public behavior.
- Keep unshipped proposals, benchmarks, release procedures, and maintainer
  notes under the repository-level `Docs` directory, not `Documentation~`.
- Use `Docs/Production.md` for source-checkout validation and release gates.
