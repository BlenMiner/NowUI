# Package Footprint Plan

> Internal design note for unshipped packaging work. It does not change the
> runtime feature set or the current Git/unitypackage layout.

## Measured baseline

`npm pack Assets/NowUI --dry-run --json` on 2026-08-07 reports:

| Package shape | Packed | Unpacked | Entries |
| --- | ---: | ---: | ---: |
| Before repository-only exclusions | 19.35 MiB | 39.29 MiB | 872 |
| Current npm package | 18.15 MiB | 36.50 MiB | 657 |

The current `.npmignore` excludes internal tests, the source-checkout showcase,
and the visual harness while retaining customer-facing `Samples~`. This is a
no-behavior-change saving of about 1.20 MiB packed and 2.79 MiB unpacked.

The remaining payload is dominated by two deliberate feature bundles:

- native plugins for every target: approximately 20.72 MiB unpacked;
- bundled fonts: approximately 11.05 MiB unpacked, including OpenMoji
  (6.70 MiB), Material Design Icons (1.17 MiB), JetBrains Mono (1.12 MiB),
  and the four-face Noto Sans family (2.56 MiB).

## Phase 1: make every export omit development payload

Npm publication now has the correct exclusions. Git URL installs and the
generated `.unitypackage` still see the repository layout under
`Assets/NowUI`, so they can retain `Tests`, `Example`, and `Editor/Harness`.

Unify the result by either moving those assemblies to repository-owned folders
outside the package root or building releases from a staging directory. Keep
their assembly names and asset GUIDs stable so source-checkout validation and
scenes continue to work. Verify all three install routes from a clean consumer
project before changing the release export.

## Phase 2: split native binaries by target

Create optional platform packages around the existing managed fallback:

- Windows: about 3.24 MiB;
- Linux: about 3.14 MiB;
- macOS: about 5.18 MiB;
- Android: about 3.53 MiB;
- iOS: about 4.32 MiB;
- WebGL: about 1.17 MiB.

The core package should keep the managed TrueType path and expose the same
runtime probing behavior when no native add-on is installed. WebGL and iOS
must retain explicit build-time validation because their libraries link
statically. UPM has no target-conditional dependency selection, so use a small
documented installer/editor prompt or require the relevant add-on explicitly;
do not make a meta-package pull every platform back in.

## Phase 3: make rich font fallbacks optional

Keep Noto Sans as the compact default and move OpenMoji, Material Design Icons,
and JetBrains Mono to optional font packs. Before moving assets, replace the
serialized fallback references on the `Resources/NowUI/NotoSans` family with a
runtime fallback-registration seam. Optional packs can register themselves
when present; without them, text remains functional and only the explicitly
optional emoji/icon/mono coverage is absent.

This refactor matters for player size as well as download size: the default
Resources font currently references the emoji and icon assets, so Unity must
include those dependencies even when an application never draws them. Merely
assigning a custom font at runtime does not strip assets already reachable from
Resources.

## Guardrails

- Do not delete platform binaries or font fallbacks from the existing package
  before the replacement install/registration path exists.
- Preserve the current public probing and managed-fallback behavior.
- Measure packed package size and a clean player build separately; they answer
  different questions.
- Keep size reporting informational until release correctness is made a gate.
- Test npm, Git URL, `.unitypackage`, Asset Store import, and one clean player
  per supported target before calling a split complete.
