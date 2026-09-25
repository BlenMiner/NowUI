# NowUI Standalone Core — M1 Design (final)

Historical design, retained for the engine shim's original contracts and decisions.
Its M2/M3 browser roadmap and milestone limits describe the plan at that time;
the original standalone WASM host and JavaScript authoring API were retired.
The current native workflow and optional browser target share C# scenes; for
asset loading, shaping and validation, use
[Native CLI](NativeCLI.md).

**Status:** binding design for M1 (engine-free core build). Supersedes the three candidate designs.
**Milestone:** M1 = the standalone solution builds with plain `dotnet build`, the chosen test subset passes with `dotnet test`
against a null render backend, and the Unity EditMode/PlayMode results are identical to the recorded baseline.

**Basis.** This document starts from candidate Design C (highest judge total, 176) and merges every graft the judges
recommended that does not contradict decisions D1–D8: the IMGUI compile-whole strategy and the streaming fetch sink, the
richer render capabilities, the sampler-update hook, the recording backend and the public-API dump gate from Design A; the
probe-pinned gradient/curve semantics, the event-buffered input marker and the M4 interface roadmap from Design B; and the
Tests-local shim list, the font-fixture format, the `isPlaying = false` test host and the per-test frame tick from
`StandaloneTestPlan.md`, which no candidate had.

**Source documents.** `UnityDependencyInventory.md` (inventory A, dispositions B, seams C, hazards D, defines F, structure G,
open questions H), `UnityValueTypeSemantics.md` (cited "VT §n"), `GradientCurveSemantics.md` (cited "GC §n"),
`StandaloneTestPlan.md` (cited "TP §n"), `ShimGapProbe.md` (measured compile-error counts).

**Errors corrected relative to the candidate designs, each re-verified against the tree before acceptance:**

| Claim | Verified | Correction |
|---|---|---|
| `QualitySettings.activeColorSpace` default | `ProjectSettings/ProjectSettings.asset:49` is `m_ActiveColorSpace: 0` | **Gamma**, not Linear (A and B were wrong). And it is **gate-observable**: `NowTextStylingTests.cs:265` builds a `new NowDrawList(NowMeshLayout.Canvas, …)`, which reaches `NowMesh.PatchTextCanvasColors` (`NowMesh.cs:1766-1786`, early-returns unless Linear) and `ShouldCompensateGradientCanvasGamma` (`NowMesh.cs:1858-1864`). A Linear default would change text tangent/colour inside the M1 test subset. |
| Gradient / AnimationCurve semantics | `GradientCurveSemantics.md` §3–§6 is itself the probe output (its closing "Verification environment" paragraph) | Take the verified semantics wholesale (16-bit key-time quantisation, whole-array rejection, `WrapMode.Default` = Loop, `ColorUtility` failure = **white**, `gray`/`pink`/`clear` rejected, `transparent` accepted, `dx < 1e-4` Hermite clamp). **Delete every proposed probe unit** — the probe has been run. |
| B's `INowOverlayHost` refactor | `NowOverlay.cs:701` `internal static NowOverlayHostScope Host(Component host, RectTransform, Camera)`, `:721` `Host(Component)`; callers `NowContextMenuOwnerLifetimeTests.cs:236,249`, `NowControlsAdvancedTests.cs:1909-1998` (7), `NowPopupUXTests.cs:540-757` (8, incl. `Host(hostObject.transform)` and the 3-arg form), `PlayMode/NowRenderingPlayModeTests.cs:4497-4670` | **Rejected.** Retyping those parameters breaks the Unity `Tests`/`NowUI.PlayModeTests` assemblies, which takes down the whole EditMode (1860) and PlayMode (163) baseline — a direct D8(c) violation. Scene stubs keep the signatures byte-identical. |
| B's "explicit static constructors are behaviour-neutral" | `NowLottieRenderer.cs:17`, `NowMarkdownImages.cs:22`, `NowLottieCache.cs:17`, `NowGUI.cs:8`, `NowIMGUIInputProvider.cs:11` have static field initialisers and no static ctor | **Rejected.** Adding a static ctor removes `beforefieldinit`, deferring initialisation and adding a class-init check on every static access. No unit in this plan adds a static constructor to any existing type. |
| `NowLottieBurstTessellator` stand-in shape | `NowLottieBurstTessellator.cs:8` namespace `NowUI.Internal`, `:23` `internal static class`, `:29` `public static bool forceScalar`, `:1273` `public static bool TryFill`, `:1320` `public static bool TryStroke(…, int cap, int join, …)`, `:1468` `internal static void InvalidateClip` | Stand-in matches exactly (A's "three internal static members at 1293-1356" was wrong). |
| `NowRemoteContent.cs` disposition | `:4` `using UnityEngine.Networking;`, `:13-278` `NowBoundedDownloadHandler`, `:284` `NowZipArchivePreflight`, consumed at `NowLottieAsset.cs:247` | Guard only the `using` and the handler class. B's "exclude the file" would break the build. |
| Analyzer diagnostic severity | `Analyzers~/NowUI.Analyzers/NowBuilderDiscardAnalyzer.cs:24,:35` both pass `DiagnosticSeverity.Warning` | NOWUI001/NOWUI002 are **warnings**, in Unity and standalone alike. |
| `Material.SetPass` | Unity returns `bool`; call sites `Now.cs:1174`, `Now.cs:2214` discard it | Declared `public bool SetPass(int pass)` (C had `void`). |
| `NowModelPreview` references | `grep` over `Runtime/` + `Extensions/` hits `NowModelPreview.cs` and `NowPipelineGraphic.cs` only | C is right that the only referencing file is excluded; A's "zero hits in Runtime" was wrong but harmless. |
| `InvariantGlobalization` | Not present in `Standalone/Directory.Build.props` (it sets `TargetFramework`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `Deterministic`, `PathMap`, `SatelliteResourceLanguages`) | C's grounding note was wrong. ICU stays on for runtime **and** tests (TP R12). |
| M1 gate size | TP §0/§3.2 | **36 files, 783 cases, 9 expected `Assert.Ignore` skips.** Not "39 files / 885 attributes" (A) and not a font-free tier-1 (B and C): 31 of the 36 gate files reach `Now.defaultFont`, so the font fixture is on the critical path. |
| Missing from all three candidates | `NowBenchmarkAllocations.cs:1-6` needs `Unity.PerformanceTesting` + `UnityEngine.Profiling.Recorder`, and four gate files consume it | Tests-local shims `PerformanceTesting.cs` and `Recorder.cs`, plus `[assembly: NonParallelizable]` / `LevelOfParallelism(1)` and the per-test frame tick, are budgeted (unit U23). |

**The Unity-visible diff this plan produces**, in full:

* 2 modified files, 3 guard regions total: `Runtime/NowFontCompiler.cs` (3 lines at :9), `Runtime/NowRemoteContent.cs` (2 regions: the `using` at :4 and the class at :8-278).
* 4 classes made `partial` with verbatim member moves into 4 new `*.Unity.cs` files (whole-file `#if !NOWUI_STANDALONE`) plus 4 new `*.Standalone.cs` files (whole-file `#if NOWUI_STANDALONE`, empty for Unity).
* 3 new stand-in files under `Runtime/Standalone/` (whole-file `#if NOWUI_STANDALONE`, empty for Unity).
* 1 new file under `Assets/NowUI/Editor/` (the asset exporter; Editor asmdef, no runtime effect).
* 0 asmdef edits, 0 file moves, 0 deletions, 0 static constructors added, 0 public-API changes.

---

## 1. Decisions on every open question

### 1.1 The decision table

| # | Question | **Decision** | Reason | Rejected alternative |
|---|---|---|---|---|
| **H.1** | Does the shim ship `ScriptableObject`? | **Yes** (settled by D4). `UnityEngine.Object` + `ScriptableObject` + `ISerializationCallbackReceiver` + the serialization attributes ship in the shim. | `NowFontAsset`, `NowThemeAsset`, `NowLottieAsset`, `NowControlRenderer`, `NowMaterialControlRenderer`, `NowFontFamily`, `NowUnityEditorControlRenderer` keep their declared base class; nine public declarations and ~140 attributes compile untouched. `ScriptableObject.CreateInstance<T>` must be `Activator`-based because `TrackingFont : NowFont` and `RecordingRenderer : NowControlRenderer` are declared in the `Tests` assembly (TP §6.10). | `#if`-swapped base-class lists (touches 9 public API declarations, D1 violation in spirit). |
| **H.2** | Now.cs screen path: host partial or shim emulation? | **Shim emulation.** `GL`, `Material.SetPass`, `Graphics.DrawMeshNow`, `Graphics.Blit`, `Graphics.ExecuteCommandBuffer`, `RenderTexture.GetTemporary/ReleaseTemporary/active` and `CommandBuffer` are implemented in `NowUI.Engine` over **one** immediate-mode `INowRenderBackend`. `CommandBuffer` is a recorder whose replay emits exactly the calls the GL path emits. `Now.cs` (4458 LOC) compiles **unchanged**. | One backend contract serves both submit paths, so M2 implements one thing. Zero edits in the largest, most public file. TP §1 confirms no gate test touches this path, so it carries no gate risk either. | `INowScreenBackend` host partial (splits Now.cs, moves 12 members, forces M2 to write an immediate interface *and* a replay path). |
| **H.3** | Engine-free font pipeline | **(a)** `Unity.Jobs`/`Unity.Collections`/`Unity.Mathematics`/`Unity.Burst` get minimal shims; `IJobParallelForExtensions.Schedule(n, batch).Complete()` runs `Execute(i)` for `i in [0,n)` on the calling thread. `NowManagedFontSession.cs` and `NowManagedFontBaker.cs` compile **unchanged**, so `NowSdfBakeJob.Execute` *is* the port. **(b)** `NowFontCompiler.cs:9` `#define NOWUI_MSDF_NATIVE` is wrapped in `#if !NOWUI_STANDALONE`; every call site takes its existing managed fallback. **(c)** `NowLottieNative` is switched off by defining the **existing** `NOWUI_VG_DISABLE_NATIVE`. **(d)** `NowTextShaper.cs` is unchanged: its `DllImport`s throw `DllNotFoundException` on desktop .NET and the existing probe at `:127` sets `s_unsupported = true`. **(e)** `DynamicSession.TryCopyAtlas(NativeArray<byte>)` compiles against the shim `NativeArray` (H.13). | D6 is met literally — no Burst/Jobs/Collections/Mathematics *packages*, sequential managed execution, native plugins compiled out — while avoiding a second rasterizer that can drift from the Unity one. `dotnet build` is the only witness needed. | Hand-porting `NowSdfBakeJob.Execute` into a managed `BakeCell` (two shared-file edits, ~120 new LOC, two kernels to keep in sync). Retained as the fallback if the lead objects to shimming the `Unity.*` namespaces. |
| **H.4** | How host-only classes referenced from core are cut | **Three mechanisms, no interface refactor in M1.** (1) **Scene stubs** in the shim (`GameObject`, `Component`, `Behaviour`, `Transform`, `RectTransform`, `Camera`) — classes with `internal` constructors, so standalone code can never produce one and every host-identity branch provably takes its `null` path. (2) **Compile the two IMGUI files whole**: `Runtime/NowGUI.cs` and `Runtime/Input/NowIMGUIInputProvider.cs` are compiled unchanged against an inert IMGUI shim. Verified surface: `NowGUI` uses only `Event.current`, `EventType.{Layout,Repaint}`, `FocusType.Passive`, `GUI.DrawTexture`, `GUILayoutUtility.GetRect`, `GUIUtility.GetControlID`, `ScaleMode.StretchToFill`, `Time.{frameCount,realtimeSinceStartup}`, `Application.isPlaying`, plus `Rect/Color/RenderTexture/Vector2`; `NowIMGUIInputProvider` adds `GUI.changed`, `GUIUtility.hotControl`, 8 more `EventType` members, 22 `KeyCode` members and 13 `Event` instance members. Neither file has any other `using` beyond `System` and `UnityEngine`, and every `NowKeyInput` reference in them is inside `#if NOWUI_INPUT_SYSTEM`. With `Event.current == null` they are dormant, exactly as in a Unity player with no IMGUI pass. (3) **Three stand-ins** under `Assets/NowUI/Runtime/Standalone/` for the remaining host statics (`NowWorldGraphic`, `NowLottieBurstTessellator`, `NowRectTransformProjection`), each whole-file `#if NOWUI_STANDALONE`. | Zero edits in NowInput (12 `is` sites), NowTextInput, NowOverlay (8 regions), NowSurfaceToScreenMapper, NowFocus, NowRaycastGate, NowInputSnapshot, NowLottieRenderer, NowFont 2256, Now 1063/1087, NowGradient 715, NowSdf 3548. Compiling the IMGUI pair removes the two stand-ins that were named after *public* types (the M3 API-surface smell) and gives the real semantics instead of an approximation, for ~130 LOC of IMGUI shim. | B's `INowNativeInputBridge` + `INowOverlayHost` + `object owningSelection` refactor: **rejected** because it breaks the Unity test assemblies (see the corrections table). A `NOWUI_UNITY_HOST` define over the same ~30 sites: rejected as the largest reviewable diff for no M1 benefit. |
| **H.5** | Assembly shape, partials, exclude list | Single `NowUI.Runtime` assembly in Unity (D1). Exactly **four** classes become `partial` with a host half: `NowLottieAsset`, `NowLottieCache` (+ nested `Entry`), `NowMarkdownImages` (+ nested `Entry`), `NowFilePicker` (+ nested `ThumbnailEntry`). Everything else in inventory B.3 either compiles unchanged or is excluded whole (`NowModelPreview.cs`). Exclude list in §2.2. | Every other split candidate turned out to compile against the shim. Coroutines, `MonoBehaviour` runners and `UnityWebRequest` are the only constructs no honest shim provides, and they are confined to those four classes. `NowFilePicker` already has an explicit static constructor (`:198`), so making it `partial` changes no initialisation semantics. | Separate core/host assemblies (needs a new friend entry; `partial` cannot span assemblies and Now/NowLayout/NowKeyBindingField already rely on partials). |
| **H.6** | Gradient / AnimationCurve fidelity | **Implement exactly as `GradientCurveSemantics.md` specifies** — that document *is* the Unity 6000.4 probe output, so no new probe is scheduled. The non-obvious rules the shim must carry are listed in §3.6 and include: 16-bit key-time quantisation `k = (ushort)(clamp01(t)*65535 + 0.5)` with `u` computed in the integer-`k` domain; whole-array rejection for 0 or >8 keys; single-key expansion to two keys at 0 and 1; `Evaluate(NaN)` = `(0,0,0,0)`; identity `GetHashCode` with content `Equals`; Fixed mode applying to alpha as well as colour; `WrapMode.Default` evaluating as **Loop** while reading back as `Default`; every other wrap value storing as `ClampForever`; `ClampForever` at ±∞ yielding **NaN**; the exact Hermite grouping with `dx` clamped up to `1e-4`; weighted Bézier with `1/3` on the unweighted side; `IndexOutOfRangeException("GetKey")`; `sizeof(Keyframe) == 32`. | `NowValueControls`/`NowInspector`/`NowText.SetGradient` are public builder API that M3 must mirror, so guarding them out would change the standalone surface. The evaluators are ~350 LOC and every constant is already pinned. | Guarding the gradient/curve editors out (changes the public API). Running another probe (the probe exists; A and C both budgeted a redundant unit). |
| **H.12** | Browser IO policy | **Deferred, with the seam declared now.** M1 compiles all `System.IO` code unchanged — correct on desktop .NET, which is where `dotnet test` runs (TP §8). `INowFetchProvider`/`INowFetchSink`/`INowFetchHandle` (streaming) are declared in `NowUI.Engine` in M1 so the four standalone halves are written once against their final shape; with no provider registered they fail fast with a named error. `INowFileSystem`, async clipboard and `NOWUI_STANDALONE_WASM` are M2. | The M1 exit criteria are compile + pure-logic tests; browser semantics are not exercised. Declaring the fetch interfaces costs ~30 lines and makes the standalone halves final. A **streaming** sink (rather than a buffered `GetBytes()`) maps 1:1 onto `fetch()` + `ReadableStream` and preserves the byte-cap-during-transfer contract that `NowBoundedDownloadHandler` implements today, so the cap logic is not reimplemented per call site. | An in-memory `INowFileSystem` injected into NowFilePicker (large split, no M1 value). A buffered fetcher (would have to be replaced in M2). |
| **H.13** | `NativeArray<T>` in signatures | **Shim it.** A struct over a pinned `byte[]` (owned) or a view over a `Texture2D` store, with the full `Copy` overload set, `AsSpan`, `Reinterpret`, `GetSubArray`, plus `NativeArrayUnsafeUtility` and `UnsafeUtility`. | Keeps `NowFont`, `NowFontCompiler`, `NowManagedFontSession`, `NowValueControls` untouched and keeps the public `TryCopyAtlas(NativeArray<byte>)` overload in the standalone API. No gate test reaches it (TP §8), so it is compile-only surface with a small semantics suite. | Converting `GetRawTextureData<T>` to `Span<T>` and `TryCopyAtlas` to `Span<byte>` (edits 3 shared files, changes a public overload). |
| **H.14** | `QualitySettings.activeColorSpace` | **Host-settable static `NowRuntime.colorSpace`, default `ColorSpace.Gamma`.** | `ProjectSettings.asset:49` is `m_ActiveColorSpace: 0` = Gamma, so Gamma reproduces today's Unity behaviour. This is not cosmetic: the gate file `NowTextStylingTests` builds a `NowMeshLayout.Canvas` draw list, so `PatchTextCanvasColors` runs in the M1 subset and a Linear default would gamma-convert text tangent/colour that Unity leaves alone. Host-settable keeps a Linear browser build possible with no core edit, and M2's framebuffer reads the same value. | A constant `Linear` (A, B) — silently changes colour maths inside the gate. |
| **H.16** | Test project and subset | **`Standalone/Tests/Tests.csproj`, `AssemblyName=Tests`, NUnit 3.14.0**, explicit `<Compile Include>` list = TP §3.1 exactly: 36 gate files + `Support/NowInputReplay.cs` + `Support/NowPopupTestDriver.cs` + `BenchmarkSupport/NowBenchmarkAllocations.cs`, plus Tests-local shims (`LogAssert`, `Unity.PerformanceTesting`, `UnityEngine.Profiling.Recorder`, the `[SetUpFixture]` host, `[assembly: NonParallelizable]`, `[assembly: LevelOfParallelism(1)]`). Gate = **783 cases, 9 expected skips**. `NowGraphEvaluationPerformanceTests` (Tier C, 18 cases) compiles and runs but is filtered out of the gate with `--filter "Category!=NowUI.Overview"`. Waves: **Wave 1** = the 12 files that never resolve a font (165 cases) as a smoke gate; **Wave 2** = the remaining 24 gate files once the font fixture lands. | The subset and its per-file requirements are already analysed file-by-file in TP §2. NUnit 3.14 keeps the classic asserts the Unity suite uses (3090 `Assert.AreEqual` etc.); NUnit 4 would require touching every test file. `Tests` is the only assembly name that inherits every `InternalsVisibleTo` grant. | A font-free "tier 1" of 28–30 files (B and C): TP §0 shows 31 of 36 gate files reach `Now.defaultFont`, so such a tier collapses on first run. Renaming/duplicating tests into a new assembly (loses the friend grants). |
| **H.7** | Standalone asset format | **One Unity Editor exporter**, `Assets/NowUI/Editor/NowStandaloneAssetExport.cs` (menu item + `-executeMethod`), writing JSON into `Standalone/Tests/Fixtures/`: `NowUI/NotoSans.family.json` + per-face `*.font.json` (`atlasInfo` verbatim, `materialTemplate` name; atlas pixels and `_fontBytes` omitted in M1 per TP §3.3), `materials.json` (the 9 non-UGUI `.mat` templates' float/vector/texture/keyword values), `shaders.json` (declared property names and pass counts parsed from the `.shader` files). `NowFontFamily`/`NowFontAsset` slots are populated by reflection from the test resource provider, exactly as `NowFontResolutionTests` already does. | An Editor script is required anyway to read the binary `.ttf.asset`; folding materials and shaders into it avoids a second, YAML-parsing tool. Exporting the prebaked `atlasInfo` (rather than re-baking from a TTF) is what makes `NowTextStylingTests`, `NowDockingTests` and `Simple.cs` reproduce Unity's metrics. | A PowerShell `.mat`-YAML parser (A). Dynamically baking NotoSans through `NowFontCompiler.TryCompile` (A) — TP §3.3 rejects it explicitly. A synthetic monospace font (TP §3.3 rejects it). |
| **H.8** | WebGL2 v1 feature scope | M2, not M1. Recorded in §11: rectangles/text/gradients/ripples/bezier first; glass behind `caps` gating; SDF scenes and SDF image fields behind float-format extension checks; Lottie via the managed scalar tessellator; ModelPreview and FilePicker excluded. | Out of M1 scope; the backend contract in §4 is what keeps it a pure add. | — |
| **H.9** | Clock / frame contract | **Host-driven** (D5). `NowRuntime.BeginFrame()` is the only incrementer of `Time.frameCount`; `Time.realtimeSinceStartup` is a **live** monotonic read (not frame-latched). In the Tests host, `frameCount` starts at **1** and an assembly-level `ITestAction` calls `BeginFrame()` exactly **once before each test**. | TP §4.2: `frameCount` must be constant *within* a test (`NowControlsTests.ImmediateTabNavigationDoesNotWaitForUnityFrameCount`, every `_scopeStartedAt == Time.frameCount` guard) but should roll over *between* tests so a fixture that forgets a `Reset()` cannot alias into the next. TP §4.2 also pins "live clock": six cases sleep 20–60 ms and compare against `realtimeSinceStartup`. | Leaving `frameCount` at 0 and constant for the whole run (all three candidates) — loses inter-test isolation. Auto-advancing per `Now.StartUI` — breaks the frame-count tests. |
| **H.10** | Coordinate conventions | Unity's conventions kept (D5): `Screen.safeArea` bottom-left, pointer pixel space bottom-left, `Matrix4x4.Ortho` bit-exact per VT §8, framebuffer y-flip owned by the backend. | Zero core edits; the three flip sites (NowScreen 65, NowSurfaceToScreenMapper 85-88, NowScreenInputProvider 178) stay as they are. | Converting the core to top-left (touches core and the Unity host). |
| **H.11** | `DEVELOPMENT_BUILD` equivalent | **Define `DEVELOPMENT_BUILD` in the Debug configuration** of every standalone project; run the gate in Debug. | TP R13: `NowMarkupTests.UnknownResultQueriesWarnOnce` and the leaked-scope diagnostics only log under `UNITY_EDITOR || DEVELOPMENT_BUILD`; without it those tests fail with "expected log not received". | Defining it in Release too (would ship diagnostics in the browser build). |
| **H.15** | Reset strategy | `[RuntimeInitializeOnLoadMethod]` is a no-op shim attribute (D7). `NowRuntime.ResetAll()` reflects **once** over registered assemblies, caches the ordered `MethodInfo[]`, and invokes them. Hosts may call `NowRuntime.RegisterAssembly(asm)` to avoid the domain scan. | 39 files carry the attribute; a reflection pass costs nothing at M1 scale and needs no core edit. A source-generated registry can replace the reflection in M2 without touching core. | A generated registry now (extra tooling for no M1 benefit). Converting the 39 methods into an explicit registry (39 core edits). |
| **H.17** | Culture | **ICU everywhere** — do not set `InvariantGlobalization` in any standalone project, runtime or tests. | `Standalone/Directory.Build.props` does not set it today (verified). TP R12: `NowNewControlsTests` calendar geometry follows `CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek` in both the test and `NowDatePicker`, so runtime and tests must agree; the safe way to agree is to change nothing. Invariant mode for wasm is an M2 decision with its own trade-off. | `InvariantGlobalization=true` (C assumed it was already on; it is not). Setting it for `Tests` only (TP R12 rejects it). |
| **H.18** | Native ABI | All shim value types are `[Serializable] [StructLayout(LayoutKind.Sequential)]` float/int structs with Unity's field order; `Color32` keeps its explicit 4-byte layout. Already true of the implemented layer. | `NowLottieNative` pins these layouts; `[Serializable]` keeps host-side `JsonUtility`-style round-tripping possible. | — |

### 1.2 Decisions on the questions the judges left open

| Question | **Decision** | Reason |
|---|---|---|
| Warnings-as-errors policy | `TreatWarningsAsErrors=false` for M1 in every project. `NoWarn` = `CS1591;CS0649;CS0169;CS0414` on `NowUI.Runtime` and the extensions (the `[SerializeField]` warnings Unity suppresses and MSBuild does not). The warning count is recorded in `Docs/Standalone/M1-Report.md` and the ratchet (`TreatWarningsAsErrors=true` plus an explicit `NoWarn`) is an M2 item. | A ratchet during bring-up would turn every shim gap into a build failure and slow the compile-driven loop (U21). Recording the count makes the ratchet a one-line change later. |
| NOWUI001/NOWUI002 outside Unity | They stay **warnings** (their declared severity). The analyzer is referenced from `NowUI.Runtime` and the seven extension projects; it is **not** referenced from `Tests` (TP R15 — `NowLayoutTests` has deliberate discards). | Matches Unity exactly. |
| Does the analyzer DLL load under the .NET 9 SDK? | Verified in U0 by building `NowUI.Runtime` with `/p:ReportAnalyzer=true` and asserting NOWUI001 fires on a throwaway discard. If it does not load, fall back to `<ProjectReference Include="…/Analyzers~/NowUI.Analyzers/NowUI.Analyzers.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`. | Silent analyzer loss would remove coverage with no error; a positive check costs one throwaway file. |
| The four allocation-zero gate cases | **Kept in the gate.** The shim carries a hard rule: no allocation on the steady-state capture path — `Mesh.Set*` reuses grown buffers, `Material`/`MaterialPropertyBlock` setters never box (typed dictionaries, no `object` values), `Texture2D.Apply` allocates nothing, `NullRenderBackend` records **counters only** (its draw ring is opt-in via `recordDraws`, default `false`), and `Debug` formats nothing when no sink consumes the level. If a case still fails after U26 triage, the escape hatch is `[Category("Allocation")]` plus a gate filter — and that requires the lead's sign-off, recorded in the M1 report. | TP §3.2/R6: on .NET 9 `GC.GetAllocatedBytesForCurrentThread` is exact, so these four cases measure every byte the shim allocates per frame. They are the cheapest possible early-warning for M2 browser performance, so they are worth keeping strict. |
| Standalone public-API divergence governance | A machine-checked manifest. `Tools/Standalone/Dump-PublicApi.ps1` reflects a target assembly's public surface into a sorted text file. Two uses: (a) **Unity gate** — dump `Library/ScriptAssemblies/NowUI.Runtime.dll` before and after the change; the diff must be **empty** (D8(d)); (b) **delta record** — dump the standalone `NowUI.Runtime.dll` and diff against the Unity one; the result is checked in as `Docs/Standalone/StandaloneApiDelta.md` and the build fails if the delta changes without that file being updated. Expected delta in M1: the `Now.Model`/`NowModel`/`NowModelPreview` family (excluded file), and `NowKeyBindingField` (Input-System-only in Unity too). | M3 generates the JS surface from Runtime metadata, so the delta is a product artifact, not an implementation detail. A checked-in delta file makes any future divergence a reviewed decision. |
| `.meta` bootstrap for the 8 new files under `Assets/NowUI` | Unit **U20**: after U14–U19 land, run `Unity.exe -batchmode -quit -projectPath <root> -logFile artifacts/local/meta-import.log` once with Unity closed, then run the EditMode/PlayMode harness. The generated `.meta` files are **staged, not committed** — the agent prints `git add` / `git commit` commands for the user. | Per the standing rule that commits are the user's call. The import must precede the harness run or Unity's file set differs from the baseline's for a reason unrelated to the change. |
| Reconciling the proposed Gradient/AnimationCurve probes | **There is no new probe.** `GradientCurveSemantics.md` closes with its verification environment: a single throw-away EditMode fixture on Unity 6000.4.0f1, since deleted, plus an offline brute-force of candidate float forms. The document is the probe output. | Removes a unit from A's and C's plans and prevents re-litigating settled values. |
| Must `Object.Destroy` defer to end of frame? | **Yes, faithfully.** `Destroy(obj)` and `Destroy(obj, t)` queue; the queue drains in `NowRuntime.EndFrame()` (and immediately when `Application.isPlaying` is `false`, matching Unity's editor behaviour). `DestroyImmediate` always destroys now. The `??`/`?.`/`??=` bypass audit was **performed** (results in §6.3) and shows every site is either followed by a fake-null test, or operates on a plain managed object, or has the identical bypass in Unity — but deferral is the faithful model and costs ~25 lines, so there is no reason to approximate. | The gate runs with `isPlaying = false`, where Unity itself destroys immediately (hazard D.1 #2 shows every core site is `isPlaying ? Destroy : DestroyImmediate`), so the gate is unaffected either way; the browser runtime is where deferral matters, and getting it right now removes a standing question. |
| Is the canvas vertex path reachable in M1? | **Yes** — `NowTextStylingTests.cs:265`. So H.14's Gamma default is verified by the gate, not deferred to M2. | See the corrections table. |
| Blend/depth/cull state for the backend | M2. `shaders.json` (H.7) gains per-pass `blend`, `zwrite`, `ztest`, `cull` parsed from the `.shader` sources; until then the WebGL2 backend hard-codes premultiplied-alpha blend, depth off, cull off, which is what all nine core programs use. Recorded in §11. | Not needed to compile or to pass the gate; needed before the first GLSL port. |
| Asynchronous image decode | M2. `ImageConversion.LoadImage` stays synchronous over `INowImageDecoder`; the **browser fetch provider pre-decodes to RGBA** before the bytes reach the shim, so `NowMarkdownImages` needs no async seam and no further split. Recorded in §11. | Keeps `NowMarkdownImages`' M1 split final. |
| Multi-canvas / multi-surface hosting in one page | M3. The scene stubs' constructors are `internal`; relaxing them (or adding `NowOverlay.Host(object)`) is a shim-only change at that point, not rework. | No M1 or M2 consumer. |
| `UNITY_INCLUDE_TESTS` in the standalone Tests project | **Not defined.** All 42 occurrences are in `NowModelPreview.cs`, which is excluded. | Verified against inventory F.1. |
| Default `LayerMask` table | Unity's default project table, host-settable: `0 Default, 1 TransparentFX, 2 Ignore Raycast, 3 "", 4 Water, 5 UI, 6..31 ""` (GC §7). | `NowMaskFieldTests` draws a `MaskField`, which enumerates layers 0..31 and skips empty names; the default table gives it the same six names the Unity editor gives it. |
| `Application.isPlaying` default | `NowRuntime.isPlaying` defaults to **`true`** (a browser/runtime host is playing); the Tests `[SetUpFixture]` sets it to **`false`** as its first statement, mirroring EditMode. | TP §4.3 requires `false` for the tests; `true` is right for a player. Making it explicit in the fixture (and asserting it in a self-test) prevents drift. |
| CPU texture copies after `Apply(_, makeNoLongerReadable: true)` | The CPU store is **kept** by default (`NowRuntime.releaseCpuCopiesOnSeal = false`); `isReadable` still becomes `false` so `GetRawTextureData` throws as in Unity. | WebGL context loss requires re-uploading every texture; font atlas pages are sealed with `Apply(false, true)` (`NowFont.cs:3465-3471`), so discarding the copy would make context loss unrecoverable without re-baking every glyph. Hosts that need the memory can flip the switch. |
| SDF bake time-slicing under wasm | M2. `IJobParallelFor.Schedule` runs synchronously in M1; whether the browser needs the bake spread across frames is measured, not guessed. Recorded in §11. | .NET wasm is single-threaded; the shim's synchronous scheduler is correct either way, and a time-sliced implementation would sit behind the same call. |
| JS command-stream ABI (opcodes, interning, result-table keying) | M3. Explicitly out of scope; §4.9 states only what M1 must guarantee for it. | The largest M3 unknown; nothing in M1 constrains it beyond keeping the builder signatures stable. |

---

## 2. Project layout under `Standalone/`

### 2.0 What exists today (do not recreate)

```
Standalone/
  Directory.Build.props          net9.0; Nullable disable; ImplicitUsings disable; TreatWarningsAsErrors false;
                                 Deterministic true; PathMap $(MSBuildThisFileDirectory)=/nowui/; SatelliteResourceLanguages en
  NowUI.Standalone.sln           2 projects today
  NowUI.Engine/NowUI.Engine.csproj   AssemblyName NowUI.Engine; RootNamespace NowUI.Engine; LangVersion latest;
                                     AllowUnsafeBlocks true; NoWarn CS1591;CS0660;CS0661;
                                     InternalsVisibleTo NowUI.Engine.Tests, Tests
  NowUI.Engine/Math/             Vector2 Vector2Int Vector3 Vector3Int Vector4 Quaternion Color Color32
                                 Rect RectInt Bounds Matrix4x4 Plane Mathf EngineClock          [IMPLEMENTED]
  NowUI.Engine/Enums/ColorSpace.cs                                                              [IMPLEMENTED]
  NowUI.Engine.Tests/            NUnit 4.2.2 + NUnit3TestAdapter + Microsoft.NET.Test.Sdk; 8 fixtures
```

`Directory.Build.props` is kept as-is. **Nothing global is added** — `LangVersion` and `DefineConstants` are per project, because the shim uses `latest` while the Unity sources must compile as C# 9.0.

### 2.1 Target tree

```
Standalone/
  Directory.Build.props                                    (exists)
  NowUI.Standalone.sln                                     (exists — gains 9 projects)
  NowUI.Engine/NowUI.Engine.csproj                         (exists — gains folders, see §3)
  NowUI.Engine.Tests/NowUI.Engine.Tests.csproj             (exists — gains the shim semantics suite, §7.5)
  NowUI.Runtime/NowUI.Runtime.csproj                       (new)
  Extensions/
    NowUI.Extensions.Markdown/NowUI.Extensions.Markdown.csproj
    NowUI.Extensions.Markup/NowUI.Extensions.Markup.csproj
    NowUI.Extensions.Markdown.Markup/NowUI.Extensions.Markdown.Markup.csproj
    NowUI.Extensions.CodeEditor/NowUI.Extensions.CodeEditor.csproj
    NowUI.Extensions.Docking/NowUI.Extensions.Docking.csproj
    NowUI.Extensions.NodeGraph/NowUI.Extensions.NodeGraph.csproj
    NowUI.Extensions.Sdf/NowUI.Extensions.Sdf.csproj
  Tests/
    Tests.csproj
    Shims/LogAssert.cs                 namespace UnityEngine.TestTools
    Shims/PerformanceTesting.cs        namespace Unity.PerformanceTesting
    Shims/Recorder.cs                  namespace UnityEngine.Profiling
    Shims/AssemblyInfo.cs              [assembly: NonParallelizable] [assembly: LevelOfParallelism(1)]
    Support/NowStandaloneTestHost.cs   [SetUpFixture] + assembly-level ITestAction (per-test BeginFrame)
    Support/NowStandaloneTestResources.cs   INowResourceProvider (materials, shaders, font family)
    Support/NowRecordingLogger.cs      INowLogger backing LogAssert
    Fixtures/NowUI/NotoSans.family.json          (exported, Wave 2)
    Fixtures/NowUI/NotoSans-{Regular,Bold,Italic,BoldItalic}.font.json   (exported, Wave 2)
    Fixtures/materials.json  Fixtures/shaders.json                      (exported, Wave 1)
Tools/Standalone/
  Compare-TestResults.ps1              NUnit XML set-difference gate
  Dump-PublicApi.ps1                   public-surface dump for the D8(d) gate and the API delta
```

### 2.2 `NowUI.Runtime/NowUI.Runtime.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>NowUI.Runtime</AssemblyName>            <!-- keeps every InternalsVisibleTo in AssemblyInfo.cs valid -->
    <RootNamespace>NowUI</RootNamespace>
    <LangVersion>9.0</LangVersion>                        <!-- the Unity project's LangVersion -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>           <!-- matches the asmdef's allowUnsafeCode -->
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <DefineConstants>$(DefineConstants);NOWUI_STANDALONE;NOWUI_VG_DISABLE_NATIVE</DefineConstants>
    <DefineConstants Condition="'$(Configuration)'=='Debug'">$(DefineConstants);DEVELOPMENT_BUILD</DefineConstants>
    <NoWarn>$(NoWarn);CS1591;CS0649;CS0169;CS0414</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../Assets/NowUI/Runtime/**/*.cs" LinkBase="Runtime" Exclude="
      ../../Assets/NowUI/Runtime/URP/**;
      ../../Assets/NowUI/Runtime/HDRP/**;
      ../../Assets/NowUI/Runtime/**/*.Unity.cs;
      ../../Assets/NowUI/Runtime/NowWorldGraphic.cs;
      ../../Assets/NowUI/Runtime/NowWorldLayoutGraphic.cs;
      ../../Assets/NowUI/Runtime/NowWorldGlassBackdrop.cs;
      ../../Assets/NowUI/Runtime/NowGraphic.cs;
      ../../Assets/NowUI/Runtime/NowLayoutGraphic.cs;
      ../../Assets/NowUI/Runtime/NowPipelineGraphic.cs;
      ../../Assets/NowUI/Runtime/NowPipelineLayoutGraphic.cs;
      ../../Assets/NowUI/Runtime/NowVisualElement.cs;
      ../../Assets/NowUI/Runtime/NowUGUINavigationProxy.cs;
      ../../Assets/NowUI/Runtime/NowEditorRebuildQueue.cs;
      ../../Assets/NowUI/Runtime/NowBootstrap.cs;
      ../../Assets/NowUI/Runtime/NowModelPreview.cs;
      ../../Assets/NowUI/Runtime/Input/NowWorldInputProvider.cs;
      ../../Assets/NowUI/Runtime/Input/NowRectTransformInputProvider.cs;
      ../../Assets/NowUI/Runtime/Input/NowKeyInput.cs;
      ../../Assets/NowUI/Runtime/Controls/NowKeyBindingField.cs;
      ../../Assets/NowUI/Runtime/Lottie/NowLottieGraphic.cs;
      ../../Assets/NowUI/Runtime/Lottie/NowLottieBurstTessellator.cs" />
    <ProjectReference Include="../NowUI.Engine/NowUI.Engine.csproj" />
    <Analyzer Include="../../Assets/NowUI/Runtime/Analyzers/NowUI.Analyzers.dll" />
  </ItemGroup>
</Project>
```

The exclude list is **18 explicit file entries** plus the `URP/**` and `HDRP/**` folder globs and the `**/*.Unity.cs`
pattern. Arithmetic: inventory B.4's 22 host-only files, minus `NowGUI.cs` and `Input/NowIMGUIInputProvider.cs` (now
compiled, H.4), minus `URP/NowUniversalRendererFeature.cs` and `HDRP/NowHighDefinitionCustomPass.cs` (covered by the
folder globs), minus `Extensions/Markup/Editor/NowMarkupBindingsGenerator.cs` (a different project's folder), = 17; plus
`NowModelPreview.cs` = **18**.
Files that are already whole-file define-guarded in Unity (`NowGraphic`, `NowVisualElement`, `NowUGUINavigationProxy`,
`NowLayoutGraphic`, `NowLottieGraphic`, `NowKeyInput`, `NowKeyBindingField`, `NowEditorRebuildQueue`) would compile to
nothing anyway; they are listed so the csproj *is* the disposition table and no reviewer has to cross-reference §5.
`Runtime/Standalone/*.Standalone.cs` and the four `*.Standalone.cs` halves are **included** by the glob.

**Symbols deliberately not defined:** `UNITY_EDITOR`, `UNITY_INCLUDE_TESTS`, `NOWUI_UGUI`, `NOWUI_UITOOLKIT`,
`NOWUI_INPUT_SYSTEM`, `ENABLE_INPUT_SYSTEM`, `ENABLE_LEGACY_INPUT_MANAGER`, `NOWUI_XR`, `NOWUI_PHYSICS`,
`NOWUI_PHYSICS2D`, `NOWUI_ANIMATION`, `NOWUI_PARTICLE_SYSTEM`, `NOWUI_URP`, `NOWUI_HDRP`, `UNITY_WEBGL`, `UNITY_IOS`,
`UNITY_STANDALONE_WIN`, `UNITY_EDITOR_WIN`, `NOWUI_STANDALONE_WASM`.

### 2.3 Extension projects (7)

Identical shape. `AssemblyName` = the asmdef name (so the `InternalsVisibleTo` grants keep working),
`RootNamespace NowUI`, `LangVersion 9.0`, `AllowUnsafeBlocks false` (none of the extension asmdefs sets it),
`EnableDefaultCompileItems false`, `DefineConstants $(DefineConstants);NOWUI_STANDALONE` (+ `DEVELOPMENT_BUILD` in Debug),
same `NoWarn`, `<Analyzer Include="../../../Assets/NowUI/Runtime/Analyzers/NowUI.Analyzers.dll" />`.

| Project (= AssemblyName) | Compile glob (`LinkBase="Extensions/<Folder>"`) | Excludes | ProjectReferences |
|---|---|---|---|
| NowUI.Extensions.Markdown | `Assets/NowUI/Extensions/Markdown/**/*.cs` | `**/*.Unity.cs` | NowUI.Runtime |
| NowUI.Extensions.Markup | `Assets/NowUI/Extensions/Markup/**/*.cs` | `Extensions/Markup/Editor/**` | NowUI.Runtime |
| NowUI.Extensions.Markdown.Markup | `Assets/NowUI/Extensions/MarkdownMarkup/**/*.cs` | — | Runtime, Markdown, Markup |
| NowUI.Extensions.CodeEditor | `Assets/NowUI/Extensions/CodeEditor/**/*.cs` | — | NowUI.Runtime |
| NowUI.Extensions.Docking | `Assets/NowUI/Extensions/Docking/**/*.cs` | — | NowUI.Runtime |
| NowUI.Extensions.NodeGraph | `Assets/NowUI/Extensions/NodeGraph/**/*.cs` | — | NowUI.Runtime |
| NowUI.Extensions.Sdf | `Assets/NowUI/Extensions/Sdf/**/*.cs` | — | NowUI.Runtime |

The Sdf asmdef carries a `NOWUI_UGUI` versionDefine; it is not defined here, so its UGUI branches compile out exactly as in
a Unity project without UGUI.

### 2.4 `Tests/Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Tests</AssemblyName>                    <!-- required: inherits every InternalsVisibleTo grant -->
    <RootNamespace>Tests</RootNamespace>
    <LangVersion>9.0</LangVersion>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>          <!-- matches the Unity Tests asmdef -->
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <DefineConstants>$(DefineConstants);NOWUI_STANDALONE</DefineConstants>
    <DefineConstants Condition="'$(Configuration)'=='Debug'">$(DefineConstants);DEVELOPMENT_BUILD</DefineConstants>
    <SignAssembly>false</SignAssembly>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />          <!-- classic Assert.*; NUnit 4 moved them to ClassicAssert -->
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../NowUI.Engine/NowUI.Engine.csproj" />
    <ProjectReference Include="../NowUI.Runtime/NowUI.Runtime.csproj" />
    <ProjectReference Include="../Extensions/NowUI.Extensions.Markdown/NowUI.Extensions.Markdown.csproj" />
    <ProjectReference Include="../Extensions/NowUI.Extensions.Markup/NowUI.Extensions.Markup.csproj" />
    <ProjectReference Include="../Extensions/NowUI.Extensions.Markdown.Markup/NowUI.Extensions.Markdown.Markup.csproj" />
    <ProjectReference Include="../Extensions/NowUI.Extensions.CodeEditor/NowUI.Extensions.CodeEditor.csproj" />
    <ProjectReference Include="../Extensions/NowUI.Extensions.Docking/NowUI.Extensions.Docking.csproj" />
    <ProjectReference Include="../Extensions/NowUI.Extensions.NodeGraph/NowUI.Extensions.NodeGraph.csproj" />
    <ProjectReference Include="../Extensions/NowUI.Extensions.Sdf/NowUI.Extensions.Sdf.csproj" />
    <!-- NO <Analyzer> reference: NowLayoutTests contains deliberate NOWUI001/002 discards (TP R15). -->
    <Compile Include="Shims/**/*.cs;Support/**/*.cs" />
    <Compile Include="../../Assets/NowUITests/Support/NowInputReplay.cs;
                      ../../Assets/NowUITests/Support/NowPopupTestDriver.cs;
                      ../../Assets/NowUITests/BenchmarkSupport/NowBenchmarkAllocations.cs"
             LinkBase="UnityTests/Support" />
    <Compile Include="@(NowUIWave1);@(NowUIWave2)" LinkBase="UnityTests" />
    <None Include="Fixtures/**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
  <ItemGroup>
    <!-- Wave 1: the 12 gate files that never resolve a font (TP R1) -->
    <NowUIWave1 Include="../../Assets/NowUITests/NowRectTests.cs;
      ../../Assets/NowUITests/NowResolvedIdTests.cs; ../../Assets/NowUITests/NowControlStateResolvedIdTests.cs;
      ../../Assets/NowUITests/NowFocusResolvedIdTests.cs; ../../Assets/NowUITests/NowOverlayResolvedIdTests.cs;
      ../../Assets/NowUITests/NowIdentityIntegrationTests.cs; ../../Assets/NowUITests/NowTextEditTests.cs;
      ../../Assets/NowUITests/NowNumericExpressionTests.cs; ../../Assets/NowUITests/NowFilePickerTests.cs;
      ../../Assets/NowUITests/NowFilePickerUserFoldersTests.cs; ../../Assets/NowUITests/NowInteractionRegionTests.cs;
      ../../Assets/NowUITests/NowInteractionRepaintTests.cs; ../../Assets/NowUITests/NowContextInputRoutingTests.cs;
      ../../Assets/NowUITests/NowFocusHostRegistryTests.cs; ../../Assets/NowUITests/NowMaskShapeTests.cs;
      ../../Assets/NowUITests/NowCornerRadiusTests.cs; ../../Assets/NowUITests/NowNodeGraphIndexedEvaluationTests.cs" />
    <!-- Wave 2: the remaining gate files (font fixture required) -->
    <NowUIWave2 Include="../../Assets/NowUITests/Simple.cs; ../../Assets/NowUITests/NowLayoutTests.cs;
      ../../Assets/NowUITests/NowFontResolutionTests.cs; ../../Assets/NowUITests/NowFontStackTests.cs;
      ../../Assets/NowUITests/NowTextWrapTests.cs; ../../Assets/NowUITests/NowTextAreaTests.cs;
      ../../Assets/NowUITests/NowTextSelectionTests.cs; ../../Assets/NowUITests/NowTextPreprocessorTests.cs;
      ../../Assets/NowUITests/NowMarkupTests.cs; ../../Assets/NowUITests/NowCodeEditorTests.cs;
      ../../Assets/NowUITests/NowDockingTests.cs; ../../Assets/NowUITests/NowViewStackTests.cs;
      ../../Assets/NowUITests/NowNewControlsTests.cs; ../../Assets/NowUITests/NowInspectorTests.cs;
      ../../Assets/NowUITests/NowDialogTests.cs; ../../Assets/NowUITests/NowNodeGraphTests.cs;
      ../../Assets/NowUITests/NowTextStylingTests.cs; ../../Assets/NowUITests/NowTextShapingTests.cs;
      ../../Assets/NowUITests/NowControlsTests.cs;
      ../../Assets/NowUITests/NowGraphEvaluationPerformanceTests.cs" />   <!-- Tier C: filtered out of the gate -->
  </ItemGroup>
</Project>
```

Wave 1 as listed is 17 files; the 12 that TP R1 names as font-free are the smoke subset inside it (`NowRectTests`,
the five id/identity files, `NowTextEditTests`, `NowNumericExpressionTests`, `NowFilePickerTests`,
`NowFilePickerUserFoldersTests`, `NowInteractionRegionTests`, `NowInteractionRepaintTests`, `NowContextInputRoutingTests`,
`NowFocusHostRegistryTests` = 165 cases). Wave 1 + Wave 2 = the 36-file gate plus the Tier C perf file.

Gate command: `dotnet test Standalone/Tests/Tests.csproj -c Debug --filter "Category!=NowUI.Overview"`.

### 2.5 `NowUI.Engine.csproj` additions

Add `<InternalsVisibleTo Include="NowUI.Runtime" />` (the stand-ins and future backends reach a few `internal` hooks such
as `Texture.IncrementUpdateCount()`), and `<InternalsVisibleTo Include="NowUI.Backend.WebGL2" />` is **not** added in M1 —
M2 adds it to `NowUI.Engine.csproj`, which is a standalone-only file, so it is not a core edit.

### 2.6 Solution

`NowUI.Standalone.sln` gains the nine new projects. `dotnet build Standalone/NowUI.Standalone.sln` builds
Engine → Runtime → 7 extensions → Tests, plus Engine.Tests. `dotnet test Standalone/NowUI.Standalone.sln` runs both test
assemblies (Engine.Tests on NUnit 4, Tests on NUnit 3 — they share no source and no package graph).

### 2.7 Proving the Unity build is unchanged (D8(c), D8(d))

Four checks, all mechanical:

1. **File set.** `git status --porcelain Assets/NowUI` must list exactly: `M Runtime/NowFontCompiler.cs`,
   `M Runtime/NowRemoteContent.cs`, `M` the four files that became `partial`, and `A` the 11 new files
   (3 stand-ins + 4 `*.Unity.cs` + 4 `*.Standalone.cs`) + 1 Editor file + their `.meta` files. No `D`, no `R`.
2. **asmdefs.** `git diff --stat -- "Assets/**/*.asmdef" "Assets/**/*.asmdef.meta"` must be empty.
3. **Public API.** `Tools/Standalone/Dump-PublicApi.ps1 -Assembly Library/ScriptAssemblies/NowUI.Runtime.dll` (plus the
   seven extension assemblies) before and after; the diff must be **empty**. This is the cheapest proof of D8(d) — but it
   is not sufficient on its own, because the seams NowUI exposes to its tests are `internal`. Therefore:
4. **Compilation of the Unity test assemblies** is implied by check 5 and is the real guard against internal-signature
   changes (the `NowOverlay.Host(Component)` class of break).
5. **Harness.** EditMode and PlayMode results compared against the recorded baseline by set difference (§7.4).

Reviewers can discharge the four verbatim member moves with `git diff -M --color-moved=zebra`; §5.2 states, per file,
which moves are byte-identical and which contain an extraction (only two, both spelled out line by line).

---

## 3. Shim specification (`NowUI.Engine`)

**Conventions.**
* Every Unity-facing type is `public` and lives in the namespace Unity puts it in (`UnityEngine`, `UnityEngine.Rendering`,
  `Unity.Collections`, …), regardless of which folder the file sits in.
* Value types are `[Serializable] [StructLayout(LayoutKind.Sequential)]` with Unity's exact field order (H.18).
* **No non-Unity public member may be added to a `UnityEngine.*` type.** Backend-facing state goes on `internal` members or
  on helper types in `NowUI.Engine`.
* Semantics come from `UnityValueTypeSemantics.md` (VT §n) and `GradientCurveSemantics.md` (GC §n). Where a member is not
  referenced by the standalone compile set or the test subset it is **omitted** and listed as omitted, so a later compile
  error is a signal rather than a surprise.
* The signatures below are the minimum contract. The compile-driven loop (U21) may add more Unity members; it may never
  add a member Unity does not have.

**Type budget:** `UnityEngine.*` 88 · `UnityEngine.Rendering` 8 · `Unity.Collections/Jobs/Burst/Mathematics/Profiling` 21 ·
`NowUI.Engine` helpers and interfaces 24 · Runtime stand-ins 3 → **144 types**, of which **16 already exist**.

### 3.1 `Engine/Math/`, `Engine/Enums/ColorSpace.cs` — ALREADY IMPLEMENTED

`Vector2`, `Vector2Int`, `Vector3`, `Vector3Int`, `Vector4`, `Quaternion`, `Color`, `Color32`, `Rect`, `RectInt`, `Bounds`,
`Matrix4x4`, `Plane`, `Mathf`, `EngineClock`, `ColorSpace` are implemented and under test (780 cases in
`NowUI.Engine.Tests`). **Do not rewrite them.** Two additions remain, plus whatever members the compile loop demands.

**`Engine/Math/BoundsInt.cs`** — `namespace UnityEngine`, `public struct BoundsInt : IEquatable<BoundsInt>`; private
`Vector3Int m_Position, m_Size`.

```csharp
public BoundsInt(Vector3Int position, Vector3Int size);
public BoundsInt(int xMin, int yMin, int zMin, int sizeX, int sizeY, int sizeZ);
public Vector3Int position { get; set; }        public Vector3Int size { get; set; }
public int x { get; set; }  y  z  sizeX  sizeY  sizeZ            // component accessors over m_Position / m_Size
public int xMin { get; }  xMax  yMin  yMax  zMin  zMax           // Min/Max of position and position+size
public Vector3Int min { get; set; }             public Vector3Int max { get; set; }
public Vector3 center { get; }
public bool Contains(Vector3Int position);                       // min-inclusive, max-exclusive
public void ClampToBounds(BoundsInt bounds);    public void SetMinMax(Vector3Int min, Vector3Int max);
public static bool operator ==(BoundsInt lhs, BoundsInt rhs);  public static bool operator !=(BoundsInt, BoundsInt);
public override bool Equals(object other);  public bool Equals(BoundsInt other);  public override int GetHashCode();
public override string ToString();                               // "Position: {0}, Size: {1}"
```

Only `NowInspector` reads `position`/`size`.

**`Engine/Math/LayerMask.cs`** — `namespace UnityEngine`, `public struct LayerMask` (GC §7: **no** `[Serializable]` and
**no** `ToString` override in 6000.4; a single private `int m_Mask`, 4 bytes).

```csharp
public int value { get; set; }
public static implicit operator int(LayerMask mask);      public static implicit operator LayerMask(int intVal);
public static string LayerToName(int layer);   // host.layerNames[layer] for 0 <= layer < 32, otherwise "" (never throws)
public static int NameToLayer(string layerName);          // ordinal, case-sensitive, untrimmed; "" and null return the
                                                          // first empty-named layer (3 with the default table) — GC §7
public static int GetMask(params string[] layerNames);    // null -> ArgumentNullException("layerNames"); ORs 1<<NameToLayer
public override bool Equals(object other);  public override int GetHashCode();    // both over m_Mask, for determinism
```

Default host table (`INowHostServices.layerNames`, 32 entries):
`Default, TransparentFX, Ignore Raycast, "", Water, UI`, then 26 empty strings.

`Plane` is implemented but currently unreferenced by the standalone compile set; keep it. `Vector3.Slerp/SlerpUnclamped/
RotateTowards/OrthoNormalize` and `Matrix4x4.rotation/decomposeProjection/LookAt/Frustum/TransformPlane` stay omitted.

### 3.2 `Engine/Enums/` — `namespace UnityEngine` unless noted

One file per group. Values are Unity's and are normative (`NowKeyInput` does arithmetic on `KeyCode`, and serialized data
can round-trip).

`Enums/HideFlags.cs` — `[Flags] HideFlags { None=0, HideInHierarchy=1, HideInInspector=2, DontSaveInEditor=4,
NotEditable=8, DontSaveInBuild=16, DontUnloadUnusedAsset=32, DontSave=52, HideAndDontSave=61 }`.

`Enums/KeyCode.cs` — the **complete** Unity `KeyCode` enum with Unity's numeric values. Core uses 34 members; ship all of
them, because a partial enum turns into a compile error at a random call site later.

`Enums/RuntimePlatform.cs` — the **complete** Unity `RuntimePlatform` enum. `NowFilePickerUserFolders.Platform` compares
against `WindowsEditor/WindowsPlayer/OSXEditor/OSXPlayer/LinuxEditor/LinuxPlayer`. `Application.platform` defaults to the
**OS-mapped desktop value** (`WindowsPlayer`/`OSXPlayer`/`LinuxPlayer`), not `WebGLPlayer`: `NowTextEditTests` asserts the
platform convention and TP §4.3 requires OS mapping. Browser hosts set `WebGLPlayer` explicitly.

`Enums/TextureEnums.cs` — `FilterMode { Point=0, Bilinear=1, Trilinear=2 }`;
`TextureWrapMode { Repeat=0, Clamp=1, Mirror=2, MirrorOnce=3 }`;
`TextureFormat { Alpha8=1, RGB24=3, RGBA32=4, ARGB32=5, RGBAHalf=17, RFloat=18, RGBAFloat=20, R8=63 }`;
`RenderTextureFormat { ARGB32=0, Depth=1, ARGBHalf=2, Shadowmap=3, RGB565=4, ARGB4444=5, ARGB1555=6, Default=7,
ARGB2101010=8, DefaultHDR=9, ARGB64=10, ARGBFloat=11, RGFloat=12, RGHalf=13, RFloat=14, RHalf=15, R8=16, ARGBInt=17,
RGInt=18, RInt=19, BGRA32=20, RGB111110Float=22, RG32=23, RGBAUShort=24, RG16=25, BGRA10101010_XR=26, BGR101010_XR=27,
R16=28 }`; `RenderTextureReadWrite { Default=0, Linear=1, sRGB=2 }`;
`VRTextureUsage { None=0, OneEye=1, TwoEyes=2, DeviceSpecific=3 }`;
`CubemapFace { Unknown=-1, PositiveX=0, NegativeX=1, PositiveY=2, NegativeY=3, PositiveZ=4, NegativeZ=5 }`;
`MeshTopology { Triangles=0, Quads=2, Lines=3, LineStrip=4, Points=5 }`; `SpriteMeshType { FullRect=0, Tight=1 }`.

`Enums/DataEnums.cs` — `GradientMode { Blend=0, Fixed=1, PerceptualBlend=2 }`;
`WrapMode { Default=0, Once=1, Clamp=1, Loop=2, PingPong=4, ClampForever=8 }` (GC §4.3 — `Clamp` and `Once` share value 1);
`WeightedMode { None=0, In=1, Out=2, Both=3 }`; `LogType { Error=0, Assert=1, Warning=2, Log=3, Exception=4 }`;
`LogOption { None=0, NoStacktrace=1 }`; `SystemLanguage` (full); `ScreenOrientation { Portrait=1, PortraitUpsideDown=2,
LandscapeLeft=3, LandscapeRight=4, AutoRotation=5 }`; `RuntimeInitializeLoadType { AfterSceneLoad=0, BeforeSceneLoad=1,
AfterAssembliesLoaded=2, BeforeSplashScreen=3, SubsystemRegistration=4 }`;
`TouchScreenKeyboardType { Default=0, ASCIICapable=1, NumbersAndPunctuation=2, URL=3, NumberPad=4, PhonePad=5,
NamePhonePad=6, EmailAddress=7, NintendoNetworkAccount=8, Social=9, Search=10, DecimalPad=11, OneTimeCode=12 }` (GC §9).

`Enums/ImguiEnums.cs` — `EventType { MouseDown=0, MouseUp=1, MouseMove=2, MouseDrag=3, KeyDown=4, KeyUp=5, ScrollWheel=6,
Repaint=7, Layout=8, DragUpdated=9, DragPerform=10, Ignore=11, Used=12, ValidateCommand=13, ExecuteCommand=14,
DragExited=15, ContextClick=16, MouseEnterWindow=20, MouseLeaveWindow=21, TouchDown=80, TouchUp=81, TouchMove=82,
TouchEnter=83, TouchLeave=84, TouchStationary=85 }`; `FocusType { Native=0, Keyboard=1, Passive=2 }`;
`ScaleMode { StretchToFill=0, ScaleAndCrop=1, ScaleToFit=2 }`;
`[Flags] EventModifiers { None=0, Shift=1, Control=2, Alt=4, Command=8, Numeric=16, CapsLock=32, FunctionKey=64 }`.

`Enums/Rendering.cs` — `namespace UnityEngine.Rendering`:
`TextureDimension { Unknown=-1, None=0, Any=1, Tex2D=2, Tex3D=3, Cube=4, Tex2DArray=5, CubeArray=6 }`;
`IndexFormat { UInt16=0, UInt32=1 }`;
`[Flags] MeshUpdateFlags { Default=0, DontValidateIndices=1, DontResetBoneBounds=2, DontNotifyMeshUsers=4,
DontRecalculateBounds=8 }`;
`VertexAttribute { Position=0, Normal=1, Tangent=2, Color=3, TexCoord0=4 … TexCoord7=11, BlendWeight=12, BlendIndices=13 }`;
`VertexAttributeFormat { Float32=0, Float16=1, UNorm8=2, SNorm8=3, UNorm16=4, SNorm16=5, UInt8=6, SInt8=7, UInt16=8,
SInt16=9, UInt32=10, SInt32=11 }`;
`BuiltinRenderTextureType { None=0, CurrentActive=1, CameraTarget=2, Depth=3, DepthNormals=4, ResolvedDepth=5 }`;
`GraphicsDeviceType { Direct3D11=2, Null=4, OpenGLES3=11, Metal=16, OpenGLCore=17, Direct3D12=18, Vulkan=21 }`.
`OpenGLCore` was added for the native capture host; value 17 was checked against Unity 6000.4.0f1's CoreModule.
`ShadowSamplingMode` and `RenderTextureMemoryless` are omitted.

### 3.3 `Engine/Attributes/Attributes.cs` — `namespace UnityEngine`

Plain attribute classes with Unity's member names, because `NowInspector` reflects over them and `NowInspectorTests`
declares them on test-assembly fields (TP §6.4).

```csharp
[AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute {}
[AttributeUsage(AttributeTargets.Field)] public sealed class SerializeReference : Attribute {}
[AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute {}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct,
                Inherited = true, AllowMultiple = true)]
public abstract class PropertyAttribute : Attribute { public int order { get; set; } }

public sealed class TooltipAttribute   : PropertyAttribute { public readonly string tooltip; public TooltipAttribute(string tooltip); }
public sealed class HeaderAttribute    : PropertyAttribute { public readonly string header;  public HeaderAttribute(string header); }
public sealed class SpaceAttribute     : PropertyAttribute { public readonly float height;   public SpaceAttribute(); public SpaceAttribute(float height); }
public sealed class RangeAttribute     : PropertyAttribute { public readonly float min, max; public RangeAttribute(float min, float max); }
public sealed class MinAttribute       : PropertyAttribute { public readonly float min;      public MinAttribute(float min); }
public sealed class TextAreaAttribute  : PropertyAttribute { public readonly int minLines, maxLines; public TextAreaAttribute(); public TextAreaAttribute(int minLines, int maxLines); }
public sealed class MultilineAttribute : PropertyAttribute { public readonly int lines;      public MultilineAttribute(); public MultilineAttribute(int lines); }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CreateAssetMenuAttribute : Attribute { public string menuName { get; set; } public string fileName { get; set; } public int order { get; set; } }

[AttributeUsage(AttributeTargets.Class)] public sealed class PreferBinarySerializationAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute {
    public RuntimeInitializeOnLoadMethodAttribute();
    public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType);
    public RuntimeInitializeLoadType loadType { get; private set; }
}

public interface ISerializationCallbackReceiver { void OnBeforeSerialize(); void OnAfterDeserialize(); }
```

`ContextMenu`, `AddComponentMenu`, `ExecuteAlways`, `RequireComponent`, `DisallowMultipleComponent` and
`DefaultExecutionOrder` are omitted (host-only files). `SerializeField` is spelled without the `Attribute` suffix on
purpose — that is Unity's own type name.

### 3.4 `Engine/Object/` and `Engine/Scene/` — the object model — `namespace UnityEngine`

**`Object.cs`** — `public class Object`, VT §13 exactly.

```csharp
static int s_nextInstanceId = -1;                        // runtime-created ids are negative and decreasing
int m_InstanceID;  string m_Name = "";  HideFlags m_HideFlags;  internal bool isDestroyed;
protected Object();
public string name { get; set; }                         // MissingReferenceException when destroyed
public HideFlags hideFlags { get; set; }                 // same
public int GetInstanceID();  public int GetEntityId();   // both return m_InstanceID; stable after destroy
public override int GetHashCode();                       // m_InstanceID
public override bool Equals(object other);               // CompareBaseObjects semantics
public static bool operator ==(Object x, Object y);      // fake-null: a destroyed object == null is true
public static bool operator !=(Object x, Object y);
public static implicit operator bool(Object exists);     // !(exists == null)
public override string ToString();                       // destroyed -> "null", else "{name} ({GetType().FullName})"
public static void Destroy(Object obj);                  public static void Destroy(Object obj, float t);
public static void DestroyImmediate(Object obj);         public static void DestroyImmediate(Object obj, bool allowDestroyingAssets);
public static void DontDestroyOnLoad(Object target) {}   // no-op
public static T Instantiate<T>(T original) where T : Object;
public static Object Instantiate(Object original);
internal virtual Object CloneForInstantiate();           // Material/Texture2D/Mesh override; others -> NotSupportedException
internal virtual void OnDestroyResources() {}            // graphics handles release backend objects here
internal static void TrackLive(Object o);                // weak list drained by NowRuntime.Shutdown
```

Destroy pipeline (§6.3): `Destroy` queues when `Application.isPlaying` is true and destroys now when it is false;
`DestroyImmediate` always destroys now. Destroying runs `OnDestroyResources()`, then (for a `ScriptableObject`) dispatches
`OnDisable` then `OnDestroy`, then sets `isDestroyed = true`. Destroying `null` or an already-destroyed object is silent.

**`Exceptions.cs`** — `public class MissingReferenceException : SystemException` with Unity's message
(`"The object of type '{0}' has been destroyed but you are still trying to access it."`) and
`public class UnityException : SystemException` (thrown by `Texture2D.GetRawTextureData` on a non-readable texture).

**`ScriptableObject.cs`** — `public class ScriptableObject : Object`.

```csharp
protected ScriptableObject();
public static ScriptableObject CreateInstance(Type type);
public static T CreateInstance<T>() where T : ScriptableObject;
```

`CreateInstance` uses `Activator.CreateInstance(type, nonPublic: true)` — **not** a registry — because `TrackingFont`,
`RecordingRenderer` and other subclasses are declared outside `NowUI.Runtime` (TP §6.10). It then dispatches `Awake()` and
`OnEnable()` through a cached per-type `MethodInfo` lookup (`BindingFlags.Instance | Public | NonPublic | DeclaredOnly`,
walking base types base-first, parameterless, `void`). `SetDirty` and `OnValidate` dispatch are omitted (editor-only).

**`Engine/Scene/SceneStubs.cs`** — identity-only types with **no public constructor**, so standalone code can never
produce one and every host-identity branch provably takes its `null` path.

```csharp
public class GameObject : Object { internal GameObject();
    public bool activeInHierarchy => false;  public bool activeSelf => false;
    public Transform transform => null;  public string tag => "";  public int layer => 0;
    public UnityEngine.SceneManagement.Scene scene => default; }

public class Component : Object { internal Component();
    public GameObject gameObject => null;  public Transform transform => null; }

public class Behaviour : Component { internal Behaviour();
    public bool enabled { get; set; }  public bool isActiveAndEnabled => false; }

public class Transform : Component { internal Transform();
    public Matrix4x4 localToWorldMatrix => Matrix4x4.identity;  public Matrix4x4 worldToLocalMatrix => Matrix4x4.identity;
    public Vector3 position => Vector3.zero;  public Quaternion rotation => Quaternion.identity;
    public Vector3 lossyScale => Vector3.one; }

public sealed class RectTransform : Transform { internal RectTransform();
    public Rect rect => default;  public Vector2 pivot => default;  public Vector2 sizeDelta => default; }

public sealed class Camera : Behaviour { internal Camera();
    public static Camera current => null;  public static Camera main => null;
    public Rect pixelRect => default;  public int pixelWidth => 0;  public int pixelHeight => 0;
    public RenderTexture targetTexture => null;
    public Matrix4x4 worldToCameraMatrix => Matrix4x4.identity;  public Matrix4x4 projectionMatrix => Matrix4x4.identity; }
```

Plus `namespace UnityEngine.SceneManagement { public readonly struct Scene { public bool IsValid() => false;
public string name => ""; public int buildIndex => -1; } }`.

`MonoBehaviour` and `Coroutine` are **deliberately not provided** — their only core users move to host halves (§5.2). That
is why `NowContextMenuOwnerLifetimeTests` stays a Tier D file, exactly as TP §2.1 classifies it.

### 3.5 `Engine/Graphics/` — resource handles — `namespace UnityEngine`

All graphics handles derive from `Object`. **No constructor touches the backend**: creation is lazy (first draw, first
upload, or `Create()`), which is what makes hazard D.1 #4 safe. Every handle carries `internal int backendId` (0 = not
created) and `internal uint version` (bumped on every CPU-side change) so a backend can cache GPU objects keyed by
`GetInstanceID()` and re-upload only on a version mismatch.

**`Shader.cs`**

```csharp
public sealed class Shader : Object {
    internal Shader(string name, NowUI.Engine.NowShaderInfo info);
    internal NowUI.Engine.NowShaderInfo info { get; }
    public static int PropertyToID(string name);       // process-wide intern table, ids from 1; NEVER reset
    internal static string IDToName(int id);           // for backends and tests
    public static Shader Find(string name);            // host.resources.FindShader(name); null when unknown
    public static void SetGlobalFloat(int nameID, float value);    SetGlobalFloat(string name, float value);
    public static void SetGlobalInt(int, int);                     SetGlobalInt(string, int);
    public static void SetGlobalVector(int, Vector4);              SetGlobalVector(string, Vector4);
    public static void SetGlobalColor(int, Color);                 SetGlobalMatrix(int, Matrix4x4);
    public static void SetGlobalTexture(int, Texture);             SetGlobalTexture(string, Texture);
    public static float GetGlobalFloat(int nameID);   public static Vector4 GetGlobalVector(int nameID);
}
```

Writes land in `NowRuntime.globals` immediately. The intern table is **never** reset — core types cache ids in
`static readonly` fields, exactly as under Unity, so `ResetAll` must leave it alone.

**`Material.cs`** — a property bag.

```csharp
public class Material : Object {
    public Material(Shader shader);              // seeds the bag from shader.info defaults; name = shader.name
    public Material(Material source);            // deep copy of bag (arrays copied), keywords, shader, name
    public Shader shader { get; set; }
    public Texture mainTexture { get; set; }     // the "_MainTex" slot; the getter returns the stored instance
                                                 // (hazard D.1 #10: NowGradient uses ReferenceEquals on it)
    public Vector2 mainTextureOffset { get; set; }  public Vector2 mainTextureScale { get; set; }   // "_MainTex_ST"
    public bool HasProperty(int nameID);  public bool HasProperty(string name);   // bag contains OR shader.info declares
    public float GetFloat(int);  float GetFloat(string);  void SetFloat(int, float);  void SetFloat(string, float);
    public int GetInt(int);  void SetInt(int, int);  void SetInteger(int, int);
    public Vector4 GetVector(int);  Vector4 GetVector(string);  void SetVector(int, Vector4);  void SetVector(string, Vector4);
    public Color GetColor(int);  Color GetColor(string);  void SetColor(int, Color);  void SetColor(string, Color);
    public Texture GetTexture(int);  Texture GetTexture(string);  void SetTexture(int, Texture);  void SetTexture(string, Texture);
    public Matrix4x4 GetMatrix(int);  void SetMatrix(int, Matrix4x4);
    public void SetVectorArray(int nameID, Vector4[] values);   void SetVectorArray(int, List<Vector4>);
    public Vector4[] GetVectorArray(int nameID);                void GetVectorArray(int, List<Vector4>);
    public void SetFloatArray(int, float[]);  void SetFloatArray(int, List<float>);  float[] GetFloatArray(int);
    public void CopyPropertiesFromMaterial(Material mat);
    public void EnableKeyword(string keyword);  void DisableKeyword(string keyword);  bool IsKeywordEnabled(string keyword);
    public string[] shaderKeywords { get; set; }
    public bool SetPass(int pass);               // records (this, pass) for Graphics.DrawMeshNow; returns true
    public int passCount { get; }                public int renderQueue { get; set; }
    internal NowUI.Engine.NowMaterialBag bag { get; }
    internal uint version;                       // ++ on every setter; backends key their uniform cache on it
    internal override Object CloneForInstantiate() => new Material(this);
    internal override void OnDestroyResources(); // backend.ReleaseMaterial(this)
}
```

`SetVectorArray` must **copy into a bag-owned array of the caller's length**, and `GetVectorArray` must return that
length: `NowMaskShader` and `NowSdf` reuse static scratch arrays across frames (8/2 mask, 64/16 SDF) and Unity copies at
call time. Allocation rule: the bag reuses its stored array when the incoming length matches (the steady-state case), so
the copy allocates nothing.

**`MaterialPropertyBlock.cs`** — a plain class (not an `Object`) over the same `NowMaterialBag` shape:
`MaterialPropertyBlock()`, `Clear()`, `bool isEmpty { get; }`, and the same typed `Set*`/`Get*` pairs with `int` and
`string` keys. `CommandBuffer.DrawMesh` **snapshots** the block at record time (Unity does too), because `NowMaskShader`
hands out one shared block for consecutive batches.

**`Texture.cs`** — `public abstract class Texture : Object`.

```csharp
public virtual int width { get; set; }   public virtual int height { get; set; }
public int mipmapCount { get; protected set; }              // 1 unless mipChain
public bool isReadable { get; protected set; }
public FilterMode filterMode { get; set; }                  // default Bilinear; setter -> backend.UpdateSampler(this)
public TextureWrapMode wrapMode { get; set; }               // default Repeat
public TextureWrapMode wrapModeU { get; set; }  wrapModeV  wrapModeW
public int anisoLevel { get; set; }
public uint updateCount { get; private set; }               // hazard D.1 #11 staleness signal
public virtual UnityEngine.Rendering.TextureDimension dimension { get; }    // Tex2D
public static int currentTextureMemory { get; }
internal void IncrementUpdateCount();   internal uint version;
```

**`Texture2D.cs`** — CPU store on the Pinned Object Heap, so `NativeArray` views need no `GCHandle`.

```csharp
public Texture2D(int width, int height);                                    // RGBA32, mipChain true  (TP §6.1)
public Texture2D(int width, int height, TextureFormat format, bool mipChain);
public Texture2D(int width, int height, TextureFormat format, bool mipChain, bool linear);
public Texture2D(int width, int height, TextureFormat format, int mipCount, bool linear);
public TextureFormat format { get; }        public bool isLinear { get; }
public Unity.Collections.NativeArray<T> GetRawTextureData<T>() where T : struct;   // view over the pinned byte[];
                                                                                   // UnityException when !isReadable
public byte[] GetRawTextureData();                                                 // copy
public void LoadRawTextureData(byte[] data);
public void LoadRawTextureData<T>(Unity.Collections.NativeArray<T> data) where T : struct;
public unsafe void LoadRawTextureData(IntPtr data, int size);
public void SetPixels32(Color32[] colors);
public void SetPixels32(int x, int y, int blockWidth, int blockHeight, Color32[] colors);   // bottom-up row placement
public void SetPixels(Color[] colors);   public Color32[] GetPixels32();   public Color[] GetPixels();
public Color GetPixel(int x, int y);     public void SetPixel(int x, int y, Color color);
public void Apply();  public void Apply(bool updateMipmaps);  public void Apply(bool updateMipmaps, bool makeNoLongerReadable);
public void Reinitialize(int width, int height);
public void Reinitialize(int width, int height, TextureFormat format, bool hasMipMap);
public static Texture2D blackTexture { get; }  whiteTexture  grayTexture  redTexture  normalTexture   // lazy 1x1, named
internal byte[] pixels;   internal RectInt dirtyRect;   internal bool uploadPending;
internal void ApplyRows(int y, int rowCount);   // NowTextureUpload: upload only rows written through GetRawTextureData
internal override void OnDestroyResources();      // backend.ReleaseTexture(this)
internal override Object CloneForInstantiate();
```

`Apply` unions the sub-rects touched since the last `Apply` into `dirtyRect`, bumps `updateCount` and `version`, and — if a
backend is installed — calls `backend.UploadTexture2D(this, pixels, dirtyRect, updateMipmaps)`; otherwise the upload
happens on first bind. `makeNoLongerReadable: true` sets `isReadable = false` but **keeps** the CPU store unless
`NowRuntime.releaseCpuCopiesOnSeal` is set (§1.2). Row order is Unity's: raw data and `SetPixels32` are bottom-up
(row 0 = bottom); the shim stores exactly what it is given and the framebuffer convention is the backend's problem, so
`NowFontCompiler.FlipRgbaRows` and `NowGradient`'s row maths behave identically.

`GetRawTextureData<T>()` can only mark the whole texture dirty. Dynamic font pages are written through it and then
`ApplyRows` narrows the upload to the band of rows a bake touched (the caller promises nothing else changed). Backends
upload a partial `dirtyRect` with a sub-image upload; a texture first uploaded partially is allocated without data, since
texels outside the written rows are never sampled. Unity builds reach the same result through a staging texture and
`Graphics.CopyTexture` (`NowUI.Internal.NowTextureUpload`).

**`RenderTexture.cs`**

```csharp
public RenderTexture(int width, int height, int depth);
public RenderTexture(int width, int height, int depth, RenderTextureFormat format);
public RenderTexture(int width, int height, int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite);
public RenderTexture(RenderTextureDescriptor desc);          public RenderTexture(RenderTexture textureToCopy);
public override int width { get; set; }   public override int height { get; set; }   // setters re-create on next Create()
public int depth { get; set; }            public RenderTextureFormat format { get; set; }
public RenderTextureDescriptor descriptor { get; set; }
public override UnityEngine.Rendering.TextureDimension dimension { get; set; }
public int volumeDepth { get; set; }      public VRTextureUsage vrUsage { get; set; }
public int antiAliasing { get; set; }     public bool bindTextureMS { get; set; }
public bool useMipMap { get; set; }       public bool autoGenerateMips { get; set; }   public bool enableRandomWrite { get; set; }
public bool Create();                     // backend.CreateRenderTexture(this); sets created
public bool IsCreated();                  // created && !backend.IsRenderTextureLost(this)
public void Release();                    // backend.ReleaseRenderTexture; created = false; descriptor kept
public void DiscardContents();  public void DiscardContents(bool discardColor, bool discardDepth);  public void MarkRestoreExpected();
public static RenderTexture active { get; set; }             // NowImmediate.activeTarget / NowImmediate.SetActive
public static RenderTexture GetTemporary(int width, int height);
public static RenderTexture GetTemporary(int width, int height, int depthBuffer);
public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format);
public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format, RenderTextureReadWrite readWrite);
public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format, RenderTextureReadWrite readWrite, int antiAliasing);
public static RenderTexture GetTemporary(RenderTextureDescriptor desc);
public static void ReleaseTemporary(RenderTexture temp);
internal override void OnDestroyResources() => Release();
```

**Temporary pool** (`Engine/Graphics/NowTemporaryRenderTexturePool.cs`) — the **only** pool; the backend has no
`GetTemporary`. Keyed by `(width, height, depth, format, readWrite, antiAliasing, dimension, volumeDepth, useMipMap)`.
`filterMode`/`wrapMode` are **not** part of the key: NowUI assigns them *after* the call
(`NowSdfImageField.cs:448-451`), so including them would only fragment the pool. `GetTemporary` pops a free match or
creates and `Create()`s one named `"TempBuffer"`; `ReleaseTemporary` pushes it back; `NowRuntime.EndFrame()` releases
entries unused for **8 frames** (an untrimmed pool is an unbounded leak — `NowSdfImageField.Bake` takes two float/half
temporaries per bake at the field's exact pixel size); `ResetAll` empties it. Binding a `RenderTexture` as a draw target
increments its `updateCount` (NowSdf staleness semantics).

**`RenderTextureDescriptor.cs`** — struct with `width, height, msaaSamples (1), volumeDepth (1), mipCount (-1),
depthBufferBits, colorFormat (RenderTextureFormat), sRGB, dimension (Tex2D), vrUsage (None), useMipMap, autoGenerateMips,
bindMS, enableRandomWrite, shadowSamplingMode`; ctors `(int,int)`, `(int,int,RenderTextureFormat)`,
`(int,int,RenderTextureFormat,int depthBufferBits)`, `(int,int,RenderTextureFormat,int,int mipCount)`.

**`Sprite.cs`** — `public sealed class Sprite : Object` with `texture`, `rect`, `textureRect`, `border`, `pivot`,
`pixelsPerUnit`, and `public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit = 100f,
uint extrude = 0, SpriteMeshType meshType = SpriteMeshType.Tight, Vector4 border = default)`. Only
`NowRectangle.SetSprite` and `NowSdf.Sprite` read it.

**`ImageConversion.cs`** — `public static class ImageConversion` with **extension methods**, because the call form is
`texture.LoadImage(bytes, markNonReadable: true)`:

```csharp
public static bool LoadImage(this Texture2D tex, byte[] data);
public static bool LoadImage(this Texture2D tex, byte[] data, bool markNonReadable);
public static byte[] EncodeToPNG(this Texture2D tex);
public static byte[] EncodeToJPG(this Texture2D tex, int quality = 75);
```

These call `NowRuntime.host.imageDecoder`, **not** the render backend: GL and image codecs are different capabilities, a
headless host wants a decoder without a renderer, and the browser decoder is `createImageBitmap`. With no decoder
installed `LoadImage` returns `false`, which is exactly `NowMarkdownImages`' existing "failed to decode" path.

**`Mesh.cs`** — retains **copies** of everything it is given, because `Now.UploadCapturedMeshes` reuses static scratch
lists across draw lists.

```csharp
public Mesh();   public void MarkDynamic() {}   public void MarkModified();
public void Clear();   public void Clear(bool keepVertexLayout);        // zero counts, keep capacity (allocation rule)
public int vertexCount { get; }   public int subMeshCount { get; set; }
public UnityEngine.Rendering.IndexFormat indexFormat { get; set; }      public Bounds bounds { get; set; }
public void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes);
public void SetVertexBufferData<T>(T[] data, int dataStart, int meshBufferStart, int count, int stream = 0, MeshUpdateFlags flags = default) where T : struct;
public void SetVertexBufferData<T>(List<T> data, …);   public void SetVertexBufferData<T>(NativeArray<T> data, …);
public void SetVertices(Vector3[] v);  SetVertices(List<Vector3>);  SetVertices(Vector3[], int start, int length);
public void SetVertices(Vector3[] v, int start, int length, MeshUpdateFlags flags);
public void SetNormals(…);  SetTangents(…);  SetColors(…);              // the same four overload shapes
public void SetUVs(int channel, Vector2[] uvs);   SetUVs(int, List<Vector2>);
public void SetUVs(int channel, Vector2[] uvs, int start, int length, MeshUpdateFlags flags);
public void SetUVs(int channel, Vector3[] uvs);   SetUVs(int, Vector4[] uvs);   SetUVs(int, List<Vector4>);
public void SetUVs(int channel, Vector4[] uvs, int start, int length, MeshUpdateFlags flags);
public void SetIndexBufferParams(int indexCount, IndexFormat format);
public void SetIndexBufferData<T>(T[] data, int dataStart, int meshBufferStart, int count, MeshUpdateFlags flags = default) where T : struct;   // + List / NativeArray
public void SetSubMesh(int index, SubMeshDescriptor desc, MeshUpdateFlags flags = default);
public void SetSubMeshes(SubMeshDescriptor[] descs, int start, int count, MeshUpdateFlags flags = default);
public SubMeshDescriptor GetSubMesh(int index);
public void SetTriangles(int[] triangles, int submesh, bool calculateBounds = true);
public int[] GetTriangles(int submesh);   public void GetTriangles(List<int> triangles, int submesh);
public void GetVertices(List<Vector3> vertices);   public void GetNormals(List<Vector3>);   public void GetTangents(List<Vector4>);
public void GetColors(List<Color32>);   public void GetUVs(int channel, List<Vector2>);   public void GetUVs(int channel, List<Vector4>);
public void RecalculateBounds();   public void UploadMeshData(bool markNoLongerReadable) {}
internal NowUI.Engine.NowMeshData data;
internal override void OnDestroyResources();   internal override Object CloneForInstantiate();
```

Read-back copies out of the stream store; for an interleaved store it de-interleaves through the descriptor table
(`Position → GetVertices`, `TexCoordN → GetUVs(N)`, dimension-aware). The getters **clear the list and append exactly
`length` items** — Unity semantics, asserted by `NowTextWrapTests` and `NowTextStylingTests`. `bounds` is stored as given.
**Allocation rule:** `Clear()` keeps capacity and every `Set*` writes into the existing buffer when it is large enough;
this is precisely what the four allocation-zero gate cases measure.

**`Engine/Graphics/Rendering/`** — `namespace UnityEngine.Rendering`:

* **`SubMeshDescriptor`** (struct): `Bounds bounds; MeshTopology topology; int indexStart, indexCount, baseVertex,
  firstVertex, vertexCount;` ctor `(int indexStart, int indexCount, MeshTopology topology = MeshTopology.Triangles)`.
  *Note for backend authors:* NowUI writes **global** indices (`Now.cs:1922-1940` appends with
  `mesh.AppendTriangles(ref _triangles16, vertexOffset)`) and sets `firstVertex = vertexOffset` with `baseVertex` left at
  0 — so a WebGL2 backend must **not** add an offset of its own.
* **`VertexAttributeDescriptor`** (struct): `VertexAttribute attribute; VertexAttributeFormat format; int dimension;
  int stream;` ctor `(VertexAttribute attribute = VertexAttribute.Position, VertexAttributeFormat format =
  VertexAttributeFormat.Float32, int dimension = 3, int stream = 0)`; `internal int byteSize`.
* **`RenderTargetIdentifier`** (struct): internal `Kind { None, BuiltinType, NameID, Texture }`; fields
  `kind, builtin, nameID, texture, mipLevel, face, depthSlice`; ctors from `BuiltinRenderTextureType`, `int nameID`,
  `Texture`, `(RenderTexture, int mip, CubemapFace, int depthSlice)`, `(RenderTargetIdentifier, int, CubemapFace, int)`;
  implicit conversions from all four; `public const int AllDepthSlices = -1`; value `==`/`!=`/`Equals`/`GetHashCode`.
  `default` is `Kind.None`, meaning "the current target".
* **`CommandBuffer`** (`IDisposable`) — an op recorder; full member list in §4.4.

**`Graphics.cs`** — `public static class Graphics`:
`ExecuteCommandBuffer(CommandBuffer)`; `DrawMeshNow(Mesh, Matrix4x4)`, `DrawMeshNow(Mesh, Matrix4x4, int materialIndex)`,
`DrawMeshNow(Mesh, Vector3, Quaternion)`, `DrawMeshNow(Mesh, Vector3, Quaternion, int)`;
`Blit(Texture source, RenderTexture dest)`, `Blit(Texture, RenderTexture, Material, int pass = -1)`,
`Blit(Texture, Material, int pass)`, `Blit(Texture, RenderTexture, Vector2 scale, Vector2 offset)`;
`SetRenderTarget(RenderTexture rt)`, `SetRenderTarget(RenderTargetIdentifier rt)`; `CopyTexture(Texture, Texture)`.
`Blit` follows Unity and **leaves the destination bound** (`RenderTexture.active == dest` afterwards); NowUI's own callers
(`NowSdfImageField.ClearTarget`, `NowSdfImageField.Bake`) save and restore `RenderTexture.active` around every blit, so
this is observable only to the semantics suite — which asserts it.

**`GL.cs`** — `public static class GL`: `PushMatrix()`, `PopMatrix()`, `LoadIdentity()`, `LoadProjectionMatrix(Matrix4x4)`,
`LoadOrtho()`, `LoadPixelMatrix()`, `LoadPixelMatrix(float,float,float,float)`, `MultMatrix(Matrix4x4)`,
`modelview { get; set; }`, `static Matrix4x4 GetGPUProjectionMatrix(Matrix4x4 proj, bool renderIntoTexture)` (an identity
transform — the backend owns clip-space conventions), `Clear(bool clearDepth, bool clearColor, Color backgroundColor)`,
`Clear(bool, bool, Color, float depth)`, `Viewport(Rect)`, `invertCulling { get; set; }`, `sRGBWrite { get; set; }`.
`Begin/End/Vertex/Color/TexCoord` are omitted (unused by NowUI).

### 3.6 `Engine/Data/` — Gradient and AnimationCurve — `namespace UnityEngine`

This is the section where "reasonable" is wrong. Every rule below is marked `[verified]` in `GradientCurveSemantics.md`.

**`GradientKeys.cs`**

```csharp
public struct GradientColorKey {                       // 20 bytes
    public Color color;  public float time;
    public GradientColorKey(Color col, float time);  public GradientColorKey(in Color col, float time); }
public struct GradientAlphaKey {                       // 8 bytes
    public float alpha;  public float time;
    public GradientAlphaKey(float alpha, float time); }
```

**`Gradient.cs`** — `public class Gradient : IEquatable<Gradient>`. Internal storage is `(Color color, ushort k)[]` and
`(float alpha, ushort k)[]`.

```csharp
public Gradient();                                 // Blend, colorSpace Uninitialized(-1), white@k0/white@k65535, 1@0/1@1
public Color Evaluate(float time);
public GradientColorKey[] colorKeys { get; set; }          public GradientAlphaKey[] alphaKeys { get; set; }
public int colorKeyCount { get; }                          public int alphaKeyCount { get; }
public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys);
public void SetKeys(ReadOnlySpan<GradientColorKey> colorKeys, ReadOnlySpan<GradientAlphaKey> alphaKeys);
public void GetColorKeys(Span<GradientColorKey> keys);     public void GetAlphaKeys(Span<GradientAlphaKey> keys);
public void SetColorKeys(ReadOnlySpan<GradientColorKey> keys);  public void SetAlphaKeys(ReadOnlySpan<GradientAlphaKey> keys);
public GradientMode mode { get; set; }             // stored in 8 bits: (GradientMode)(-1) reads back as 255
public ColorSpace colorSpace { get; set; }         // full int; default Uninitialized (-1)
public override bool Equals(object o);  public bool Equals(Gradient other);  public override int GetHashCode();
```

Rules (GC §3):

1. **Write:** `k = (ushort)(Mathf.Clamp01(time) * 65535f + 0.5f)`; read-back time is `k / 65535f`. So `0.5` round-trips to
   `0.5000076 [0x3F000080]`, `0.25 → 0.2500038`, `0.75 → 0.7499962`, while `0.2` and `0.6` are exact.
2. **Validation is per array and silent:** an array is applied only when `1 <= count <= 8`. `null`, 0, 9 or 10 keys is
   **ignored entirely** — the previous keys are kept and the *other* array is still applied if it is valid. Never clamp
   to 8.
3. **A single key expands to two** at `k = 0` and `k = 65535`, discarding the key's own time.
4. Keys are kept **stable-sorted by `k`**; equal times preserve insertion order.
5. The getters allocate a **fresh array on every access** (NowUI sorts the returned array in place).
6. **Search** (`Evaluate`, colour and alpha arrays independently):
   `if (float.IsNaN(time)) return new Color(0, 0, 0, 0);`
   `float tq = time * 65535f;`  `tq = Mathf.Clamp(tq, key[0].k, key[n-1].k);`
   `i` = the largest index with `key[i].k <= tq`, then `i = Mathf.Min(i, n - 2)`;
   `float denom = (float)(key[i+1].k - key[i].k);`  `float u = denom > 0f ? (tq - key[i].k) / denom : 0f;`
   **Computing `u` from the `k / 65535f` floats does not reproduce Unity** — the fixed-point form above is the only one
   that matched all 34 captured bit patterns.
7. **Blend:** per channel `result = a + (b - a) * u`, single precision, nothing clamped (HDR and negative components pass
   through).
8. **Fixed:** `tq <= key[i].k ? key[i] : key[i+1]` — and this applies to the **alpha** keys too (alpha steps).
9. **PerceptualBlend:** the same search and `u`; alpha is still the linear lerp; colour is mixed in Oklab with the 2021
   constants (both matrices are in GC §3.6), with an sRGB→linear input transfer that uses `c^2.2` for components above 1
   when `colorSpace != ColorSpace.Linear`, the inverse transfer on output, and in-gamut results quantised to `n/255`. A
   ±1-byte deviation in the red channel is documented and accepted; GC §3.6's sample table is the regression fixture.
10. **`Equals`** is a content comparison (colour keys including quantised times, alpha keys, `mode`, `colorSpace`), but
    **`GetHashCode()` is `RuntimeHelpers.GetHashCode(this)`** — an identity hash, so equal gradients never collide in a
    `Dictionary`. That is Unity's behaviour, `NowGradientRampCache` works around it with its own comparer, and a
    content-based hash would be a silent divergence.

**`Keyframe.cs`** — `public struct Keyframe` with private fields in declaration order
`float m_Time, m_Value, m_InTangent, m_OutTangent; int m_TangentMode; int m_WeightedMode; float m_InWeight, m_OutWeight;`
(**32 bytes** — asserted by the semantics suite). Public `time, value, inTangent, outTangent, inWeight, outWeight`
(float get/set), `weightedMode` (`WeightedMode`, stored as the raw int so `7` and `-1` round-trip) and `[Obsolete]
tangentMode` (int, round-trips). Constructors: `(time, value)` → tangents 0, weights 0, `None`;
`(time, value, inTangent, outTangent)` → weights 0, `None`;
`(time, value, inTangent, outTangent, inWeight, outWeight)` → **`weightedMode = Both`**.

**`AnimationCurve.cs`** — `public class AnimationCurve : IEquatable<AnimationCurve>`.

```csharp
public AnimationCurve();   public AnimationCurve(params Keyframe[] keys);
public float Evaluate(float time);
public Keyframe[] keys { get; set; }     // getter copies (an empty curve returns the same empty instance);
                                         // setter stable-sorts by time; null -> empty
public int length { get; }               public Keyframe this[int index] { get; }   // IndexOutOfRangeException("GetKey")
public WrapMode preWrapMode { get; set; }   public WrapMode postWrapMode { get; set; }   // default ClampForever
public int AddKey(float time, float value);   public int AddKey(Keyframe key);
public int MoveKey(int index, Keyframe key);  public void RemoveKey(int index);   public void ClearKeys();
public void GetKeys(Span<Keyframe> keys);     public void SetKeys(ReadOnlySpan<Keyframe> keys);
public void SmoothTangents(int index, float weight);
public static AnimationCurve Constant(float timeStart, float timeEnd, float value);
public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd);
public static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd);
public void CopyFrom(AnimationCurve other);
public override bool Equals(object o);  public bool Equals(AnimationCurve other);  public override int GetHashCode();
```

Rules (GC §5):

1. **Wrap-mode setters normalise:** `Loop (2)` and `PingPong (4)` store as given, `Default (0)` stores as `0`, and
   **everything else** (`Once`/`Clamp` = 1, `ClampForever` = 8, 3, 16, -1) stores as `ClampForever (8)`.
2. **Evaluate:** 0 keys → `0f` for any `t` including NaN; 1 key → its value for any `t` and any wrap mode.
3. **Wrapping** with `t0 = K[0].time`, `t1 = K[n-1].time`, `range = t1 - t0`, and
   `mode = t < t0 ? preWrapMode : postWrapMode`:
   * **`ClampForever`** (and everything normalised to it): evaluate the edge polynomial with coefficients `(0, 0, 0)`,
     i.e. `x * (x * (x * 0f + 0f) + 0f) + edgeValue` where `x = t - edgeTime`. For finite `t` this is the edge value; for
     `t = ±∞` it is **NaN** (`0 · ∞`), and NaN input gives NaN. A plain clamp would be wrong.
   * **`Loop (2)` and `Default (0)`** — Default evaluates exactly like Loop: `x = (t - t0) mod range` folded into
     `[0, range)`, then `t' = t0 + x`.
   * **`PingPong (4)`**: `x = (t - t0) mod (2 * range)` into `[0, 2*range)`; `if (x > range) x = 2 * range - x;`
     then `t' = t0 + x`.
4. **Segment select:** `i` = the largest index with `K[i].time <= t'`, clamped to `[0, n-2]`; with duplicate times this
   picks the **last** duplicate as `L`.
5. **Step:** if `L.outTangent` or `R.inTangent` is infinite — any `+∞` returns `L.value`; `-∞` with no `+∞` returns
   `R.value`. Only the segment's own two tangents matter. NaN tangents fall through to the polynomial.
6. **Weighted** (when `(L.weightedMode & Out) != 0 || (R.weightedMode & In) != 0`, bit-tested on the raw int): a cubic
   Bézier with `ow = (L.weightedMode & Out) != 0 ? L.outWeight : 1f/3f`,
   `iw = (R.weightedMode & In) != 0 ? R.inWeight : 1f/3f`,
   `P0 = (L.time, L.value)`, `P1 = (L.time + dt*ow, L.value + dt*ow*L.outTangent)`,
   `P2 = (R.time - dt*iw, R.value - dt*iw*R.inTangent)`, `P3 = (R.time, R.value)`; solve `Bx(s) = t'` (Newton seeded at
   the linear `u`, bisection fallback, ~1e-7) and return `By(s)`. Bit-exactness is not achievable here and not required;
   GC §5.8's sample table is the fixture at 1e-6.
7. **Unweighted cubic Hermite — use this exact grouping** (GC §5.6, bit-exact over 22 samples):

```csharp
float dx = R.time - L.time;
if (dx < 1e-4f) dx = 1e-4f;                 // minimum-segment clamp, pinned by a 1e-7-long segment
float len = 1f / dx;
float d = R.value - L.value, m1 = L.outTangent, m2 = R.inTangent;
float a = (m1 + m2 - 2f * d * len) * (len * len);
float b = (3f * d - (2f * m1 + m2) * dx) / dx / dx;
float c = m1;
float x = tPrime - L.time;                  // NOT clamped by the 1e-4 rule
return x * (x * (x * a + b) + c) + L.value;
```

8. `AddKey(Keyframe)` returns `-1` and changes nothing when a key already exists at that time (the docs' "replaces" is
   wrong for 6000.4). `AddKey(float, float)` uses the same duplicate rule, otherwise inserts `Keyframe(time, value)` with
   `inWeight = outWeight = 1f/3f`, then applies `SmoothTangents(j, 0)` to `j = i-1, i, i+1` where those exist.
9. `MoveKey` with a time collision against a **different** key **removes** the moved key and returns `-1`.
10. `SmoothTangents(i, w)`: `tangent = 0.5f * (1f + w) * sL + 0.5f * (1f - w) * sR` (`w` is **not** clamped; an end key
    uses its single available slope for both), assigned to both tangents; `inWeight = 1f/3f` when `i > 0`,
    `outWeight = 1f/3f` when `i < n-1`; `weightedMode` and `tangentMode` are untouched.
11. `Linear`/`EaseInOut`/`Constant`: `timeStart == timeEnd` produces a one-key curve. `Linear` computes
    `tangent = (valueEnd - valueStart) / (timeEnd - timeStart)` and passes
    `{ Keyframe(timeStart, valueStart, 0, tangent), Keyframe(timeEnd, valueEnd, tangent, 0) }` through the sorting
    constructor — so `Linear(1, 0, 0, 1)` is an ease curve, not a line.
12. `Equals` is content over every key field **and** both wrap modes, ignoring `tangentMode`; `GetHashCode` is content
    over the key fields, **ignores** the wrap modes, **includes** `tangentMode`, and returns **0** for an empty curve.

### 3.7 `Engine/Services/` — `namespace UnityEngine`

Every service reads `NowRuntime.host`, which is never null (`NowRuntime`'s static constructor installs a
`DefaultHostServices`).

* **`Time.cs`** (static): `int frameCount`, `float realtimeSinceStartup`, `double realtimeSinceStartupAsDouble`,
  `float time`, `double timeAsDouble`, `float unscaledTime`, `float deltaTime`, `float unscaledDeltaTime`,
  `float smoothDeltaTime`, `float fixedDeltaTime` (0.02), `float timeScale` (1). `frameCount` and the delta fields are
  written **only** by `NowRuntime.BeginFrame()`. `realtimeSinceStartup(AsDouble)` reads `host.clock` **at call time**
  (TP §4.2: not frame-latched — six tests sleep and compare against it).
* **`Screen.cs`** (static): `int width`, `int height`, `float dpi`, `Rect safeArea`, `bool fullScreen`,
  `ScreenOrientation orientation`, `Resolution currentResolution` → `host.screen`, re-read on every access. Defaults
  1920×1080, dpi 96, `safeArea = (0, 0, w, h)` with a bottom-left origin.
* **`Application.cs`** (static): `bool isPlaying` (→ `NowRuntime.isPlaying`), `RuntimePlatform platform`,
  `string persistentDataPath`, `string dataPath`, `string streamingAssetsPath`, `bool isEditor => false`,
  `bool isBatchMode`, `int targetFrameRate { get; set; }`, `string unityVersion => "0.0.0-nowui-standalone"`,
  `string version`, `bool isMobilePlatform`, `SystemLanguage systemLanguage`, `static event Action quitting`,
  `static event Func<bool> wantsToQuit`, `void Quit()` → `NowRuntime.Shutdown()`.
* **`Debug.cs`** (static): `Log(object)`, `Log(object, Object)`, `LogWarning` ×2, `LogError` ×2, `LogAssertion` ×2,
  `LogException(Exception)`, `LogException(Exception, Object)`,
  `LogFormat`/`LogWarningFormat`/`LogErrorFormat(string, params object[])`, `Assert(bool)`, `Assert(bool, string)`,
  `bool isDebugBuild` — all funnelled into `host.logger.Log(LogType, string, Exception, Object)`.
  **`LogException` must format as `$"{ex.GetType().Name}: {ex.Message}"`**: `NowTextPreprocessorTests` matches on exactly
  that string through `LogAssert` (TP §2.2).
* **`SystemInfo.cs`** (static): `int maxTextureSize`, `bool SupportsRenderTextureFormat(RenderTextureFormat)`,
  `bool SupportsTextureFormat(TextureFormat)`, `bool supportsMultisampledTextures`,
  `int GetRenderTextureSupportedMSAASampleCount(RenderTextureDescriptor)`, `bool supportsComputeShaders => false`,
  `bool supportsInstancing`, `string graphicsDeviceName`, `GraphicsDeviceType graphicsDeviceType`,
  `int graphicsMemorySize`, `string operatingSystem` — all from `NowRuntime.backend.caps`.
* **`QualitySettings.cs`** (static): `ColorSpace activeColorSpace => NowRuntime.colorSpace` (**default Gamma**),
  `int antiAliasing`, `int vSyncCount`.
* **`Resources.cs`** (static): `T Load<T>(string path) where T : Object`, `Object Load(string path)`,
  `Object Load(string path, Type systemTypeInstance)`, `T[] LoadAll<T>(string path)`, `void UnloadAsset(Object)`,
  `void UnloadUnusedAssets()` → `host.resources`.
* **`TouchScreenKeyboard.cs`**: `public class TouchScreenKeyboard` with nested
  `enum Status { Visible = 0, Done = 1, Canceled = 2, LostFocus = 3 }`; `static bool isSupported =>
  NowRuntime.host.touchKeyboard != null`; the **eight explicit `Open` overloads** of GC §9 (Unity has no optional
  parameters there); instance `string text { get; set; }`, `bool active { get; set; }`, `Status status { get; }`,
  `TouchScreenKeyboardType type { get; }`, `int characterLimit { get; set; }`; static `bool hideInput`, `bool visible`,
  `Rect area`. With no host keyboard, `isSupported` is false and NowUI never constructs one.
* **`ExpressionEvaluator.cs`**: `public class ExpressionEvaluator { public static bool Evaluate<T>(string expression,
  out T value) { value = default; return false; } }`. Returning `false` routes `NowNumericExpression` to its own bounded
  parser, which its 61 tests already exercise. **TP R9 is the acceptance risk:** if any of the 61 golden values came from
  Unity's evaluator and the fallback disagrees, the fix is to port the evaluator (GC §8 specifies it completely, ~300
  LOC), **not** to edit a test.
* **`ColorUtility.cs`**: `public partial class ColorUtility` (a class with static members; **not** `static`, **not**
  `sealed` — GC §6).
  * `public static bool TryParseHtmlString(string htmlString, out Color color)`: null or empty → `false`; the input is
    **trimmed**; a leading `#` must be followed by 3, 4, 6 or 8 hex digits (3/4 expand by digit doubling; 6 implies alpha
    `FF`); otherwise the whole trimmed string is matched **case-insensitively** against exactly these 23 names —
    `red cyan blue darkblue lightblue purple yellow lime fuchsia white silver grey black orange brown maroon green olive
    navy teal aqua magenta transparent` (`transparent` = `00000000`). **`gray`, `pink`, `clear` and `light blue` fail.**
    Bare hex without `#` fails. Conversion is `byte / 255f`. **On failure `color` is `Color.white`, not `default`.**
  * `public static string ToHtmlStringRGB(Color color)` / `ToHtmlStringRGBA(Color color)`: per channel
    `(byte)Mathf.Clamp(Mathf.RoundToInt(c * 255f), 0, 255)` (banker's rounding), formatted `"{0:X2}{1:X2}{2:X2}"`
    upper-case with no `#`; the RGBA variant appends the alpha byte. Non-finite inputs are special-cased to `0` for Mono
    parity (GC §6.2 and §10.2).

### 3.8 `Engine/IMGUI/Imgui.cs` — `namespace UnityEngine`

New in this design (H.4). Inert by construction: `Event.current` is `null` in the standalone build, so `NowGUI.cs` and
`NowIMGUIInputProvider.cs` compile whole and every entry point returns immediately — exactly as in a Unity player with no
IMGUI pass.

```csharp
public sealed class Event {
    public static Event current { get; set; }               // settable so a host or a test can drive it; null by default
    public Event();  public Event(Event other);  public Event(int displayIndex);
    public EventType type { get; set; }        public EventType rawType { get; }
    public Vector2 mousePosition { get; set; } public Vector2 delta { get; set; }
    public int button { get; set; }            public int clickCount { get; set; }
    public KeyCode keyCode { get; set; }       public char character { get; set; }
    public EventModifiers modifiers { get; set; }
    public bool shift { get; set; }  control { get; set; }  alt { get; set; }  command { get; set; }   // over modifiers
    public bool isKey { get; }  public bool isMouse { get; }  public bool isScrollWheel { get; }
    public EventType GetTypeForControl(int controlID);      // type, or Used when already used
    public void Use();                                      // type = EventType.Used
}

public static class GUIUtility {
    public static int hotControl { get; set; }              public static int keyboardControl { get; set; }
    public static int GetControlID(FocusType focus);
    public static int GetControlID(int hint, FocusType focus);
    public static int GetControlID(int hint, FocusType focus, Rect position);
    public static string systemCopyBuffer { get; set; }     // host.clipboard, null-safe (returns "", swallows the set)
    public static Vector2 GUIToScreenPoint(Vector2 guiPoint);  public static Vector2 ScreenToGUIPoint(Vector2 screenPoint);
}

public static class GUILayoutUtility {
    public static Rect GetRect(float width, float height);
    public static Rect GetRect(float width, float height, params GUILayoutOption[] options);
    public static Rect GetLastRect();
}

public sealed class GUILayoutOption { internal GUILayoutOption(); }

public static class GUI {
    public static bool changed { get; set; }
    public static void DrawTexture(Rect position, Texture image);
    public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode);
    public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend);
    public static Color color { get; set; }   public static Color contentColor { get; set; }
}
```

`GetControlID` returns a monotonically increasing per-frame id (reset by `NowRuntime.BeginFrame`); `DrawTexture` is a
no-op. None of it is reachable while `Event.current` is `null`, but all of it is well-defined, so a future host — or the
two deferred IMGUI test files — can drive it without further shim work.

### 3.9 `Engine/Collections/`, `Engine/Jobs/`, `Engine/Burst/`, `Engine/Mathematics/`, `Engine/Profiling/`

**`Engine/Collections/NativeArray.cs`** — `namespace Unity.Collections`,
`public struct NativeArray<T> : IDisposable, IEnumerable<T>, IEquatable<NativeArray<T>> where T : struct`.
Storage: `internal byte[] buffer; internal int byteOffset; internal int length;` — owned buffers come from
`GC.AllocateUninitializedArray<byte>(n, pinned: true)` (Pinned Object Heap, so `GetUnsafePtr` needs no `GCHandle`); views
alias a `Texture2D` store.

```csharp
public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory);
public NativeArray(T[] array, Allocator allocator);   public NativeArray(NativeArray<T> array, Allocator allocator);
public T this[int index] { get; set; }   public int Length { get; }   public bool IsCreated { get; }
public void Dispose();   public Span<T> AsSpan();   public ReadOnlySpan<T> AsReadOnlySpan();   public T[] ToArray();
public void CopyFrom(T[] array);   public void CopyFrom(NativeArray<T> array);
public void CopyTo(T[] array);     public void CopyTo(NativeArray<T> array);
public NativeArray<U> Reinterpret<U>(int expectedTypeSize) where U : struct;
public NativeArray<T> GetSubArray(int start, int length);
public static void Copy(NativeArray<T> src, NativeArray<T> dst);                    // + (…, int length)
public static void Copy(NativeArray<T> src, int srcIndex, NativeArray<T> dst, int dstIndex, int length);
public static void Copy(T[] src, NativeArray<T> dst);                               // + (…, int length) + indexed
public static void Copy(NativeArray<T> src, T[] dst);                               // + (…, int length) + indexed
public bool Equals(NativeArray<T> other);  public override bool Equals(object);  public override int GetHashCode();
public Enumerator GetEnumerator();                                                  // struct enumerator, no allocation
```

Same file: `enum Allocator { Invalid = 0, None = 1, Temp = 2, TempJob = 3, Persistent = 4, AudioKernel = 5 }`,
`enum NativeArrayOptions { UninitializedMemory = 0, ClearMemory = 1 }`, and the attributes `[ReadOnly]`, `[WriteOnly]`,
`[NativeDisableParallelForRestriction]`, `[DeallocateOnJobCompletion]`.

**`Engine/Collections/UnsafeUtility.cs`** — `namespace Unity.Collections.LowLevel.Unsafe`:
`static class NativeArrayUnsafeUtility { unsafe void* GetUnsafePtr<T>(NativeArray<T>); GetUnsafeReadOnlyPtr<T>;
GetUnsafeBufferPointerWithoutChecks<T>; NativeArray<T> ConvertExistingDataToNativeArray<T>(void*, int, Allocator); }` and
`static class UnsafeUtility { MemCpy(void* dst, void* src, long size); MemMove; MemClear; MemSet(void*, byte, long);
int SizeOf<T>(); int AlignOf<T>(); ref TTo As<TFrom, TTo>(ref TFrom); ref T AsRef<T>(void*); void* AddressOf<T>(ref T);
void* Malloc(long size, int alignment, Allocator); void Free(void*, Allocator); }` — implemented over
`System.Runtime.CompilerServices.Unsafe`, `Buffer.MemoryCopy` and `NativeMemory`.

**`Engine/Jobs/Jobs.cs`** — `namespace Unity.Jobs`: `interface IJob { void Execute(); }`,
`interface IJobParallelFor { void Execute(int index); }`,
`struct JobHandle : IEquatable<JobHandle> { public void Complete(); public bool IsCompleted { get; }
public static void CompleteAll(ref JobHandle a, ref JobHandle b); public static JobHandle CombineDependencies(JobHandle a,
JobHandle b); }`,
`static class IJobExtensions { void Run<T>(this T job) where T : struct, IJob; JobHandle Schedule<T>(this T job,
JobHandle dependsOn = default) where T : struct, IJob; }`,
`static class IJobParallelForExtensions { JobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount,
JobHandle dependsOn = default) where T : struct, IJobParallelFor; void Run<T>(this T jobData, int arrayLength) where T :
struct, IJobParallelFor; }`.
**Execution is synchronous on the calling thread at `Schedule` time.** The job struct is copied by value exactly as Unity
does and results flow through `NativeArray` buffers, so there is no observable difference beyond timing.

**`Engine/Burst/Burst.cs`** — `namespace Unity.Burst`:
`[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Assembly)]
public class BurstCompileAttribute : Attribute` with plain `FloatMode`, `FloatPrecision`, `CompileSynchronously` and
`DisableSafetyChecks` properties plus the two enums; and `BurstDiscardAttribute`.

**`Engine/Mathematics/Mathematics.cs`** — `namespace Unity.Mathematics`:
`struct float2 { public float x, y; ctors; + - * / for (float2,float2), (float2,float), (float,float2); unary -;
implicit from Vector2; explicit to Vector2 }`,
`struct float4 { public float x, y, z, w; ctors including (float) and (float3,float); the same operator set; implicit
from Vector4 }`, `struct float3` (add only if the compile loop asks), and
`static class math { dot(float2,float2); dot(float4,float4); saturate(float); saturate(float4); min; max; sqrt; abs;
floor; ceil; round; clamp; lerp; length(float2); lengthsq(float2); normalize(float2); rcp; sign; select }`, implemented
with `MathF`. `NowSdfBakeJob` uses only `dot`, `saturate`, `min`, `sqrt` and the operators, all plain IEEE in Burst's
default (non-fast-math) mode, so the bake output is bit-identical to a Unity run with Burst disabled.

**`Engine/Profiling/ProfilerMarker.cs`** — `namespace Unity.Profiling`:
`public readonly struct ProfilerMarker { public ProfilerMarker(string name); public ProfilerMarker(ProfilerCategory
category, string name); public string Name { get; } public void Begin(); public void Begin(UnityEngine.Object context);
public void End(); public AutoScope Auto(); public readonly struct AutoScope : IDisposable { public void Dispose(); } }`
plus `public readonly struct ProfilerCategory { public static ProfilerCategory Scripts { get; } Render Gui Internal }`.
`AutoScope` must be a **public readonly struct**, because `NowEffects` stores one in a public struct field. Markers route
to `NowRuntime.profilerSink` (`INowProfilerSink { void Begin(string name); void End(string name); }`), which is `null` by
default and therefore free.

`namespace UnityEngine.Profiling` is **not** shipped by `NowUI.Engine`: `Recorder` is needed only by
`NowBenchmarkAllocations`, so it lives in `Standalone/Tests/Shims/Recorder.cs` (TP §3.1).

### 3.10 `NowUI.Engine` namespace — helper types (not Unity types)

| Type | Kind | Contents |
|---|---|---|
| `NowMaterialBag` | sealed class | `Dictionary<int,float> floats; Dictionary<int,int> ints; Dictionary<int,Vector4> vectors; Dictionary<int,Vector4[]> vectorArrays; Dictionary<int,float[]> floatArrays; Dictionary<int,Matrix4x4> matrices; Dictionary<int,Texture> textures; HashSet<string> keywords;` plus `CopyFrom(NowMaterialBag)`, `Clear()`, `uint version`. Typed dictionaries, never `object` — the allocation rule forbids boxing. |
| `NowShaderGlobals` | sealed class | The same dictionaries plus `int version` (incremented on every write) and `Reset()`. One process-wide instance at `NowRuntime.globals`; backends key their uniform cache on `version`. |
| `NowShaderInfo` | sealed class | `string name; int passCount; Dictionary<int,float> floatDefaults; Dictionary<int,Vector4> vectorDefaults; HashSet<int> textureSlots; string[] keywords;` — supplied by the resource provider so `Material.HasProperty` answers as Unity does for `_NowPremultipliedTexture` (`Now.cs:1020`), `_NowUITextOutlineOnlyPass` (`NowFont.cs:1478`), `_NowUITextSdfEncoding` and `_NowSdfAbiVersion` (`NowSdf.cs:3508`). |
| `NowMeshData` | sealed class | `bool interleaved; VertexAttributeDescriptor[] layout; byte[] vertexBytes; int vertexStride; NowMeshStream[] streams (8); byte[] indexBytes; IndexFormat indexFormat; int indexCount; int vertexCount; SubMeshDescriptor[] subMeshes; uint version;` |
| `NowMeshStream` | struct | `byte[] bytes; int elementSize; int count;` |
| `NowScreenInfo` | readonly struct | `int width, height; float dpi; Rect safeArea;` (bottom-left origin) |
| `NowRenderCaps`, `NowRenderTarget`, `NowRenderTextureRequest`, `NowImageFormat` | struct / enum | §4.1 |
| `NowCommandOp`, `NowCommandContext` | struct / sealed class | §4.4 |
| `NowImmediate` | internal static class | §4.4 |
| `NowTemporaryRenderTexturePool` | internal static class | §3.5 |
| `NowRuntime` | public static class | §4.6 |
| `DefaultHostServices`, `NowStopwatchClock`, `NowConsoleLogger`, `NowMemoryClipboard`, `NowEmptyResourceProvider` | sealed classes | §4.5 |
| `NullRenderBackend`, `NowRecordingRenderBackend` | sealed classes | §4.7 |
| `INowHostServices`, `INowClock`, `INowLogger`, `INowClipboard`, `INowTouchKeyboard`, `INowTouchKeyboardSession`, `INowResourceProvider`, `INowImageDecoder`, `INowFetchProvider`, `INowFetchSink`, `INowFetchHandle`, `INowProfilerSink`, `INowRenderBackend` | interfaces | §4 |

### 3.11 Runtime stand-ins — `Assets/NowUI/Runtime/Standalone/`

Three files, each **whole-file `#if NOWUI_STANDALONE`** so Unity compiles them to nothing, each with a header comment
saying it is a standalone stand-in for a host-only type and that the M4 interface refactor deletes it.

| File | Declares (namespace and accessibility copied from the real file) | Members |
|---|---|---|
| `NowWorldGraphic.Standalone.cs` | `namespace NowUI { internal static class NowWorldGraphic }` | `internal static void ReleaseCachedMaterial(Material source) {}` — called from `Now.cs:1063`, `Now.cs:1087`, `NowFont.cs:2256`, `NowGradient.cs:715` and `NowSdf.cs:3548` (five sites). |
| `NowLottieBurstTessellator.Standalone.cs` | `namespace NowUI.Internal { internal static class NowLottieBurstTessellator }` | `public static bool forceScalar;`<br>`public static bool TryFill(NowLottieContourSet contours, List<NowLottiePolyline> clipPolylines, bool clipInvert, bool evenOdd, in NowLottiePaint paint, NowLottieDrawBuffer buffer, float aaWidth, float gradientSpan, float tolerance) => false;`<br>`public static bool TryStroke(NowLottieContourSet contours, float width, int cap, int join, in NowLottiePaint paint, NowLottieDrawBuffer buffer, float aaWidth, float tolerance) => false;`<br>`internal static void InvalidateClip(List<NowLottiePolyline> clipPolylines) {}`<br>Returning `false` selects the existing scalar tessellator path in `NowLottieRenderer`. |
| `NowRectTransformProjection.Standalone.cs` | `namespace NowUI { internal static class NowRectTransformProjection }` | `public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera camera, out Vector2 localPoint) { localPoint = default; return false; }`<br>`public static Vector2 WorldToScreenPoint(Camera camera, Vector3 worldPoint) => new Vector2(worldPoint.x, worldPoint.y);` |

Before writing each file, copy the real declaration's namespace, accessibility and full parameter list from the source
(line numbers in §5.3). A mismatch is a compile error rather than a silent divergence — that is the point.

### 3.12 File map

| Folder | Files |
|---|---|
| `Engine/Math/` | *(15 exist)* + `BoundsInt.cs`, `LayerMask.cs` |
| `Engine/Enums/` | *(`ColorSpace.cs` exists)* + `HideFlags.cs`, `KeyCode.cs`, `RuntimePlatform.cs`, `TextureEnums.cs`, `DataEnums.cs`, `ImguiEnums.cs`, `Rendering.cs` |
| `Engine/Attributes/` | `Attributes.cs` |
| `Engine/Object/` | `Object.cs`, `ScriptableObject.cs`, `Exceptions.cs` |
| `Engine/Scene/` | `SceneStubs.cs` |
| `Engine/Graphics/` | `Shader.cs`, `Material.cs`, `MaterialPropertyBlock.cs`, `Texture.cs`, `Texture2D.cs`, `RenderTexture.cs`, `RenderTextureDescriptor.cs`, `NowTemporaryRenderTexturePool.cs`, `Sprite.cs`, `ImageConversion.cs`, `Mesh.cs`, `Graphics.cs`, `GL.cs`, `NowImmediate.cs` |
| `Engine/Graphics/Rendering/` | `SubMeshDescriptor.cs`, `VertexAttributeDescriptor.cs`, `RenderTargetIdentifier.cs`, `CommandBuffer.cs`, `NowCommandOp.cs` |
| `Engine/Data/` | `GradientKeys.cs`, `Gradient.cs`, `Keyframe.cs`, `AnimationCurve.cs` |
| `Engine/Services/` | `Time.cs`, `Screen.cs`, `Application.cs`, `Debug.cs`, `SystemInfo.cs`, `QualitySettings.cs`, `Resources.cs`, `TouchScreenKeyboard.cs`, `ExpressionEvaluator.cs`, `ColorUtility.cs` |
| `Engine/IMGUI/` | `Imgui.cs` |
| `Engine/Collections/` | `NativeArray.cs`, `UnsafeUtility.cs` |
| `Engine/Jobs/` | `Jobs.cs` |
| `Engine/Burst/` | `Burst.cs` |
| `Engine/Mathematics/` | `Mathematics.cs` |
| `Engine/Profiling/` | `ProfilerMarker.cs` |
| `Engine/Backend/` | `INowRenderBackend.cs`, `NowRenderCaps.cs`, `NullRenderBackend.cs`, `NowRecordingRenderBackend.cs` |
| `Engine/Host/` | `INowHostServices.cs`, `INowFetch.cs`, `DefaultHostServices.cs` |
| `Engine/` | `NowRuntime.cs`, `NowShaderGlobals.cs`, `NowMaterialBag.cs`, `NowShaderInfo.cs`, `NowMeshData.cs` |

---

## 4. Backend and host-service interfaces — `namespace NowUI.Engine`

**Design rule: one immediate-mode render contract.** The shim's `CommandBuffer` records and replays into it; the
`GL`/`DrawMeshNow` path calls it directly. The shim owns **all** CPU-side state — render targets, viewport, matrices,
shader globals, the temporary-RT pool, material bags, mesh streams — so a backend is a thin GPU adapter with no policy.
Handles are passed **directly**: there is no per-draw snapshot struct and no per-draw dictionary copy, because this is the
hot path of a renderer whose entire point is 60 fps in a browser.

### 4.1 `INowRenderBackend`

```csharp
namespace NowUI.Engine
{
    public readonly struct NowRenderCaps
    {
        public readonly int maxTextureSize;
        public readonly int maxMsaaSamples;                 // 1 = no MSAA
        public readonly bool supportsMultisampledTextures;
        public readonly bool supportsTextureArrays;
        public readonly bool supportsInstancing;
        public readonly bool supportsR8, supportsRHalf, supportsRFloat, supportsRGHalf, supportsRGFloat;
        public readonly bool supportsARGBHalf, supportsARGBFloat, supportsARGB32, supportsDepth;
        public readonly bool renderTargetsAreBottomUp;       // informational; the backend still owns the flip
        public readonly UnityEngine.ColorSpace colorSpace;   // what the framebuffer expects; SystemInfo and
                                                             // QualitySettings must agree with this
        public readonly string deviceName;
        public readonly UnityEngine.Rendering.GraphicsDeviceType deviceType;
        public readonly int graphicsMemorySizeMb;

        public bool SupportsRenderTextureFormat(UnityEngine.RenderTextureFormat format);
        public bool SupportsTextureFormat(UnityEngine.TextureFormat format);
    }

    /// <summary>A resolved draw target. A null texture means the host's default framebuffer.</summary>
    public readonly struct NowRenderTarget
    {
        public readonly UnityEngine.RenderTexture texture;   // null -> back buffer
        public readonly int mipLevel;                        // 0
        public readonly UnityEngine.CubemapFace face;
        public readonly int depthSlice;                      // -1 = all slices
        public readonly int width, height;                   // back-buffer size comes from INowHostServices.screen
    }

    /// <summary>Everything a backend needs to create a render texture, including the layout hints a WebGL2 backend may
    /// have to flatten (array slices, MSAA). A backend that flattens must report it through caps.</summary>
    public readonly struct NowRenderTextureRequest
    {
        public readonly int width, height, depthBits, volumeDepth, mipCount, msaaSamples;
        public readonly UnityEngine.RenderTextureFormat format;
        public readonly UnityEngine.RenderTextureReadWrite readWrite;
        public readonly UnityEngine.Rendering.TextureDimension dimension;
        public readonly UnityEngine.VRTextureUsage vrUsage;
        public readonly bool bindMS, useMipMap, autoGenerateMips, enableRandomWrite;
    }

    public enum NowImageFormat { Png = 0, Jpg = 1 }

    public interface INowRenderBackend
    {
        NowRenderCaps caps { get; }

        // ---- frame boundaries (called by NowRuntime) ----
        void BeginFrame(int frameCount);
        void EndFrame();

        // ---- resource lifetime; all idempotent; identity is Object.GetInstanceID() ----
        void UploadTexture2D(UnityEngine.Texture2D texture, ReadOnlySpan<byte> pixels,
                             UnityEngine.RectInt dirtyRect, bool generateMips);   // pixels = the full CPU store;
                                                                                  // dirtyRect is bottom-up texel space
        void UpdateSampler(UnityEngine.Texture texture);          // filterMode / wrapMode / anisoLevel changed
        void ReleaseTexture(UnityEngine.Texture texture);
        bool CreateRenderTexture(UnityEngine.RenderTexture texture, in NowRenderTextureRequest request);
        bool IsRenderTextureLost(UnityEngine.RenderTexture texture);   // true after a context loss
        void ReleaseRenderTexture(UnityEngine.RenderTexture texture);
        void ReleaseMesh(UnityEngine.Mesh mesh);
        void ReleaseMaterial(UnityEngine.Material material);
        bool ResolveShader(UnityEngine.Shader shader);            // program lookup by shader.name; false -> Shader.Find null

        // ---- state (shared by CommandBuffer replay and the immediate GL/Graphics path) ----
        void SetRenderTarget(in NowRenderTarget target);
        void SetViewport(in UnityEngine.Rect pixelRect);
        void SetViewProjection(in UnityEngine.Matrix4x4 view, in UnityEngine.Matrix4x4 projection);
        void ClearRenderTarget(bool clearDepth, bool clearColor, in UnityEngine.Color color, float depth);

        // ---- draws (mesh, material and block are read through the handles; globals through NowRuntime.globals) ----
        void DrawMesh(UnityEngine.Mesh mesh, int subMesh, in UnityEngine.Matrix4x4 model,
                      UnityEngine.Material material, int pass, UnityEngine.MaterialPropertyBlock properties);
        void DrawProcedural(in UnityEngine.Matrix4x4 model, UnityEngine.Material material, int pass,
                            UnityEngine.MeshTopology topology, int vertexCount, int instanceCount,
                            UnityEngine.MaterialPropertyBlock properties);
        void Blit(UnityEngine.Texture source, in NowRenderTarget destination, UnityEngine.Material material, int pass,
                  in UnityEngine.Vector2 scale, in UnityEngine.Vector2 offset,
                  int sourceDepthSlice, int destinationDepthSlice);   // material == null -> a plain copy
        void CopyTexture(UnityEngine.Texture source, UnityEngine.Texture destination);
    }
}
```

**What the shim guarantees a backend** (these are the invariants a WebGL2 author may rely on):

1. `Mesh.data.version`, `Texture.version` and `Material.version` change whenever CPU data changed, so a backend caches by
   instance id and re-uploads only on a version mismatch. `NowShaderGlobals.version` does the same for the globals bag.
2. `Material` bags are fully resolved (property id → value) and `shader.info` names the program and its declared
   properties.
3. `NowRuntime.globals` holds the current global uniforms at draw time.
4. Temporary render textures are ordinary `RenderTexture`s — a backend **never** sees a `nameID`, and there is exactly one
   temporary pool, on the shim side.
5. **`SetRenderTarget` is always followed by `SetViewport`.**
6. **Every draw is preceded by at least one `SetViewProjection`.**
7. `Blit` leaves the destination bound (Unity's convention).
8. All calls arrive on one thread, in order, between `BeginFrame` and `EndFrame`.

Image decode/encode is **not** on this interface — it is `INowImageDecoder` on the host services (§4.5).

### 4.2 `NowImmediate` — the shim-side state machine

```csharp
internal static class NowImmediate
{
    internal static NowRenderTarget activeTarget;                   // RenderTexture.active returns activeTarget.texture
    internal static Matrix4x4 modelView, projection;                // identity by default
    static readonly Stack<(Matrix4x4 mv, Matrix4x4 proj)> s_matrixStack;
    internal static (Material material, int pass) activePass;       // set by Material.SetPass

    internal static void SetActive(RenderTexture rt);               // resolve + backend.SetRenderTarget + full viewport;
                                                                    // bumps rt.updateCount
    internal static void PushMatrix();  PopMatrix();  LoadIdentity();  LoadProjection(Matrix4x4);  MultMatrix(Matrix4x4);
    internal static void DrawMeshNow(Mesh mesh, in Matrix4x4 model, int subMesh);
        // backend.SetViewProjection(modelView, projection); backend.DrawMesh(mesh, subMesh, model,
        //                                                                   activePass.material, activePass.pass, null)
    internal static void Clear(bool depth, bool color, in Color c, float z);
    internal static void Blit(Texture src, RenderTexture dst, Material mat, int pass, Vector2 scale, Vector2 offset);
    internal static NowRenderTarget Resolve(in RenderTargetIdentifier id, NowCommandContext ctx);
    internal static void Reset();                                   // called by NowRuntime.ResetAll
}

internal sealed class NowCommandContext
{
    internal readonly Dictionary<int, RenderTexture> temporaries;   // pooled; cleared per execution
    internal NowRenderTarget current;   internal Rect viewport;
}
```

`Resolve`: `Kind.None` → the current target; `CameraTarget`/`CurrentActive` → the back buffer (`texture == null`);
`NameID` → `ctx.temporaries[nameID]`; `Texture` → that `RenderTexture` (a `Texture2D` as a target throws
`ArgumentException`, as Unity does).

### 4.3 `CommandBuffer` and its replay

```csharp
public class CommandBuffer : IDisposable
{
    public CommandBuffer();
    public string name { get; set; }   public int sizeInBytes { get; }
    public void Clear();   public void Release();   public void Dispose();
    public void SetRenderTarget(RenderTargetIdentifier rt);
    public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel, CubemapFace cubemapFace, int depthSlice);
    public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth);
    public void SetViewport(Rect pixelRect);
    public void SetViewProjectionMatrices(Matrix4x4 view, Matrix4x4 proj);
    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor);
    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth);
    public void GetTemporaryRT(int nameID, int width, int height, int depthBuffer, FilterMode filter, RenderTextureFormat format);
    public void GetTemporaryRT(int nameID, int width, int height, int depthBuffer, FilterMode filter, RenderTextureFormat format, RenderTextureReadWrite readWrite);
    public void GetTemporaryRT(int nameID, int width, int height, int depthBuffer, FilterMode filter, RenderTextureFormat format, RenderTextureReadWrite readWrite, int antiAliasing);
    public void GetTemporaryRT(int nameID, RenderTextureDescriptor desc, FilterMode filter);
    public void ReleaseTemporaryRT(int nameID);
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material);
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex);
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass);
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass, MaterialPropertyBlock properties);
    public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount);
    public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount, int instanceCount);
    public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount, int instanceCount, MaterialPropertyBlock properties);
    public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest);
    public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat);
    public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass);
    public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Vector2 scale, Vector2 offset);
    public void Blit(Texture source, RenderTargetIdentifier dest);
    public void Blit(Texture source, RenderTargetIdentifier dest, Material mat, int pass);
    public void SetGlobalFloat(int nameID, float value);        SetGlobalInt(int, int);
    public void SetGlobalVector(int nameID, Vector4 value);     SetGlobalColor(int, Color);   SetGlobalMatrix(int, Matrix4x4);
    public void SetGlobalTexture(int nameID, RenderTargetIdentifier value);   SetGlobalTexture(int nameID, Texture value);
    // + the string-keyed overload of each SetGlobal*
    public void BeginSample(string name);   public void EndSample(string name);
    internal void Execute(INowRenderBackend backend);
}
```

Ops live in a `List<NowCommandOp>` — a struct with an `Op` enum and payload fields. `MaterialPropertyBlock` payloads are
**snapshot copies** (Unity copies too, and `NowMaskShader` reuses one block); `Vector4[]` and `Texture` values are stored
by reference (Unity's `SetGlobalTexture` semantics). `Clear()` drops ops and keeps capacity; `Release()` clears and marks
released — later use throws `ObjectDisposedException`, matching `NowRenderer`'s `ThrowIfDisposed`.

`Execute` walks the ops in order: `GetTemporaryRT` → `RenderTexture.GetTemporary(desc)` into `ctx.temporaries[nameID]`
(re-getting the same id replaces); `ReleaseTemporaryRT` → `ReleaseTemporary`; `SetRenderTarget` → resolve, backend, full
viewport; `SetViewport`; `SetViewProjectionMatrices`; `ClearRenderTarget`; `DrawMesh`/`DrawProcedural` → backend with the
snapshotted block; `Blit` → resolve source and destination, `backend.Blit`, destination stays bound; `SetGlobal*` →
`NowRuntime.globals` **at replay time** (Unity's ordering for a single-threaded executor). After the last op, any
temporaries still allocated are released (Unity frees them at end of execution), and
`Graphics.ExecuteCommandBuffer` restores the previously active target so `Now.cs:2158/2170` sees what it expects. The
executor allocates nothing per op (the resolution dictionary is pooled).

### 4.4 Host services

```csharp
namespace NowUI.Engine
{
    public interface INowClock      { double realtimeSeconds { get; } }        // monotonic; Stopwatch by default,
                                                                               // performance.now()/1000 in a browser
    public interface INowLogger     { void Log(UnityEngine.LogType type, string message, Exception exception,
                                               UnityEngine.Object context); }
    public interface INowClipboard  { string GetText(); void SetText(string text); }
    public interface INowProfilerSink { void Begin(string name); void End(string name); }

    public interface INowTouchKeyboardSession {
        UnityEngine.TouchScreenKeyboard.Status status { get; }
        string text { get; set; }   bool active { get; set; }
    }
    public interface INowTouchKeyboard {
        INowTouchKeyboardSession Open(string text, UnityEngine.TouchScreenKeyboardType type,
                                      bool autocorrection, bool multiline, bool secure);
    }

    public interface INowResourceProvider {
        UnityEngine.Object Load(string path, Type type);    // "NowUI/UIMaterial", "NowUI/NotoSans"; null when missing
        UnityEngine.Shader FindShader(string name);         // "NowUI/UI Rectangle"; null when unknown
    }

    public interface INowImageDecoder {
        bool TryDecode(ReadOnlySpan<byte> encoded, out int width, out int height, out byte[] rgba32BottomUp, out string error);
        byte[] TryEncode(UnityEngine.Texture2D texture, NowImageFormat format, int quality);   // null when unsupported
    }

    // Streaming fetch: maps 1:1 onto fetch() + ReadableStream, and preserves the byte-cap-during-transfer contract
    // that NowBoundedDownloadHandler implements in the Unity build.
    public readonly struct NowFetchRequest {
        public readonly string url, method;
        public readonly IReadOnlyDictionary<string, string> headers;
        public readonly int timeoutSeconds;
        public readonly bool followRedirects;                // NowUI applies its own per-hop policy, so this is false
    }
    public enum NowFetchOutcome { Success, ConnectionError, ProtocolError, Aborted, Timeout, LimitExceeded }
    public interface INowFetchSink {
        void OnResponse(long statusCode, IReadOnlyDictionary<string, string> headers);
        void OnContentLength(ulong contentLength);
        bool OnData(byte[] buffer, int count);               // returning false aborts (UnityWebRequest's ReceiveData rule)
        void OnComplete(NowFetchOutcome outcome, string error);
    }
    public interface INowFetchHandle : IDisposable { bool isDone { get; } void Abort(); }
    public interface INowFetchProvider { INowFetchHandle Start(in NowFetchRequest request, INowFetchSink sink); }

    public interface INowHostServices
    {
        INowClock clock { get; }
        NowScreenInfo screen { get; }              // re-read on every access
        INowLogger logger { get; }                 // never null
        INowClipboard clipboard { get; }           // may be null
        INowTouchKeyboard touchKeyboard { get; }   // null -> TouchScreenKeyboard.isSupported == false
        INowResourceProvider resources { get; }    // never null (an empty provider by default)
        INowImageDecoder imageDecoder { get; }     // may be null -> ImageConversion.LoadImage returns false
        INowFetchProvider fetch { get; }           // may be null -> remote loads fail fast with a named error
        UnityEngine.RuntimePlatform platform { get; }
        string persistentDataPath { get; }
        string dataPath { get; }
        string[] layerNames { get; }               // 32 entries
    }
}
```

`DefaultHostServices` (installed by `NowRuntime`'s static constructor): a `Stopwatch` clock; 1920×1080 at 96 dpi with a
full-rect safe area; a `Console`-backed logger; no clipboard; no touch keyboard; an empty resource provider (every `Load`
and `FindShader` returns null); no image decoder; no fetch provider; the OS-mapped desktop `RuntimePlatform`;
`Path.Combine(Path.GetTempPath(), "NowUI")`; Unity's default layer table. Tests and browsers replace only what they need.

### 4.5 `NowRuntime`

```csharp
namespace NowUI.Engine
{
    public static class NowRuntime
    {
        public static INowHostServices host { get; }                 // never null
        public static INowRenderBackend backend { get; }             // never null (NullRenderBackend by default)
        public static NowShaderGlobals globals { get; }
        public static UnityEngine.ColorSpace colorSpace { get; set; } = UnityEngine.ColorSpace.Gamma;   // H.14
        public static bool isPlaying { get; set; } = true;           // the Tests host sets false
        public static bool releaseCpuCopiesOnSeal { get; set; }      // false: keep CPU stores for context-loss recovery
        public static bool deferDestroyToEndOfFrame { get; set; } = true;   // debug switch; see §6.3
        public static INowProfilerSink profilerSink { get; set; }
        public static event Action onFrame;                          // per-frame ticks registered by standalone halves

        public static void Initialize(INowHostServices host, INowRenderBackend backend);
        public static void RegisterAssembly(Assembly assembly);      // avoids the ResetAll domain scan
        public static void BeginFrame();                             // frameCount++, sample the clock into Time,
                                                                     // backend.BeginFrame, reset per-frame IMGUI ids,
                                                                     // raise onFrame
        public static void EndFrame();                               // drain the destroy queue, trim the temp-RT pool,
                                                                     // backend.EndFrame
        public static void ResetAll();                               // §6.2
        public static void Shutdown();                               // Application.quitting, ResetAll, release every
                                                                     // live Object, restore the default host/backend
    }
}
```

`onFrame` is what makes the browser loop `NowRuntime.BeginFrame(); using (Now.StartUI(scale)) app.Draw();
NowRuntime.EndFrame();` sufficient — the standalone halves (`NowLottieCache.Tick`, `NowMarkdownImages.Tick`) subscribe
themselves, so no host has to name them.

### 4.6 `NullRenderBackend` and `NowRecordingRenderBackend`

**`NullRenderBackend`** — implements everything as a validated no-op that counts.
`caps` = `maxTextureSize 16384`, `maxMsaaSamples 1`, no multisampled textures, texture arrays supported, every
`RenderTextureFormat` and `TextureFormat` supported, `renderTargetsAreBottomUp = true`,
`colorSpace = NowRuntime.colorSpace`, `deviceName "Null"`, `deviceType Null`.
`CreateRenderTexture` returns true and records the id in a `HashSet<int>` so `IsRenderTextureLost` is false for created
targets and `ReleaseRenderTexture` removes them (this is what keeps `NowSdf`/`NowEffects` targets valid).
`ResolveShader` returns true for any shader the resource provider created.
`UploadTexture2D` increments `textureUploads` and stores the last dirty rect. `DrawMesh`/`DrawProcedural`/`Blit`/`Clear`
increment counters and record `lastRenderTarget`/`lastViewProjection`.
It **never throws on valid input**, but it **does** validate the way a real backend would (`null` mesh or material →
`ArgumentNullException`; sub-mesh out of range → `ArgumentOutOfRangeException`), so tests catch NowUI-side misuse.
**It allocates nothing per draw:** the optional draw ring (`recordDraws`, default `false`, capacity 256 of
`NowDrawRecord { int meshId, subMesh, materialId, pass, targetId; Matrix4x4 model; }`) is off unless a test turns it on.
`Reset()` clears counters.

**`NowRecordingRenderBackend : INowRenderBackend`** — wraps `NullRenderBackend` and appends a human-readable op log
(`IReadOnlyList<string>`). It exists for two reasons: the CommandBuffer-replay tests in the semantics suite, and the M2
acceptance harness — *the WebGL2 backend's op log must match the recording backend's op log for the same NowUI frame*.
That diff is the cheapest possible M2 correctness check and costs ~60 lines now.

### 4.7 How M2 (the WebGL2 backend) plugs in — with no core change

`NowUI.Backend.WebGL2` is a separate project (.NET 9 `browser-wasm`, `[JSImport]` bindings) implementing
`INowRenderBackend`:

* `CreateRenderTexture` builds an FBO per `RenderTexture` instance id, gating formats on `EXT_color_buffer_float` /
  `EXT_color_buffer_half_float` and reporting the result through `caps` (which is what keeps `NowSdfImageField` and
  `NowGlassRenderer` on their supported paths — `NowGlassRenderer` skips the resolve passes when
  `supportsMultisampledTextures` is false, and `NowSdf` falls back when `R8` is unavailable). `NowRenderTextureRequest`
  carries `dimension`, `volumeDepth`, `vrUsage`, `msaaSamples` and `bindMS` so a backend that must flatten an array or
  MSAA layout can do so and report it.
* `SetRenderTarget` binds the FBO or the default framebuffer; `SetViewport` follows every bind (invariant 5).
* `SetViewProjection` stores the two matrices; each draw uploads `proj * view * model`. The y-flip is applied for FBO
  targets only — NowUI's `Ortho(0, w, -h, 0, -1, 100)` plus its negated vertex `y` convention stays untouched, and
  `UNPACK_FLIP_Y_WEBGL` stays **false** because NowUI's texture rows are already bottom-up.
* `DrawMesh` uploads the mesh into a VAO keyed by `(instanceId, data.version)`; both the interleaved `RenderVertexLayout`
  and the 8-stream fallback map directly onto `vertexAttribPointer` from the `VertexAttributeDescriptor[]`. Indices are
  already global, so **no `baseVertex` offset** (§3.5). The program is looked up by `material.shader.name`; uniforms come
  from `material.bag`, the snapshotted block and `NowRuntime.globals`, resolved to names through `Shader.IDToName` and
  cached on `(material.version, globals.version)`. Blend is premultiplied alpha. Then `drawElements` over the sub-mesh
  range with `UNSIGNED_SHORT`/`UNSIGNED_INT`.
* `DrawProcedural` → `drawArrays` (the full-screen triangle uses `gl_VertexID`).
* `Blit` → a textured quad with the material's program, or a plain copy program when `material == null`.
* `IsRenderTextureLost` reports WebGL context loss, which is exactly the signal `NowSdf.cs:4731-4744`,
  `NowSdfImageAtlas.cs:66-75` and `NowSdfImageField.cs:30-33` already poll.

The browser host implements `INowHostServices` (`performance.now`, canvas size × `devicePixelRatio`, `console`, a
paste-event clipboard cache, a `fetch`-based `INowFetchProvider` that **pre-decodes images to RGBA** before handing bytes
to the shim, a resource provider over the exported font/material/shader manifest) and `INowInputProvider` (the
`NowUIToolkitInputProvider` event-buffer pattern fed from DOM pointer/wheel/key events). Its loop is
`NowRuntime.BeginFrame(); using (Now.StartUI(scale)) app.Draw(); NowRuntime.EndFrame();` — the same
`Now.StartUI` screen path Unity's built-in RP uses, because `GL`, `DrawMeshNow` and the legacy glass replay all resolve
to the same backend. **Nothing in `Assets/NowUI` changes.**

### 4.8 How M3 (the JS mirror) plugs in — with no core change

The JS mirror sits **above** the builder API: JS records builder calls into a per-frame binary command buffer; at the next
`BeginFrame` the wasm side decodes and replays them against the real static API inside one `Now.StartUI` frame; values JS
reads synchronously (`bool clicked = NowLayout.Button(...)`) come from a **previous-frame result table**. From M1 it needs
only: stable public builder signatures (unchanged — enforced by the public-API delta gate, §1.2),
`NowRuntime.BeginFrame`/`EndFrame`, `NowRuntime.ResetAll` for hot reload, and the `[NowBuilder]`/`[NowConsumer]`/
`[NowScope]` attributes as the reflection source for generating the JS surface (they are `public` in `NowUI` and are not
touched). The render backend is invisible to M3.

Two M1 decisions exist specifically to keep M3 clean: compiling `NowGUI`/`NowIMGUIInputProvider` rather than shipping
inert public stand-ins (so the generated JS surface contains no fake types), and the checked-in public-API delta.

### 4.9 The documented M4 cleanup (specified now, implemented later)

When the host-identity refactor eventually lands it deletes the three stand-ins and the scene stubs. The interfaces are
specified here so that work is a lookup rather than a redesign, and so nothing in M1 is built in a way that blocks them:

* `internal interface INowNativeInputBridge` — `isHostBacked`, `reportsNativeScreenCoordinates`, `isolatedModalDomain`,
  `excludeFromPointerArbiter`, the five `Notify*` methods, `NotifyPointerCaptured`,
  `CancelTrackedCapture(bool releaseNativeCapture)`, `TryGetTextInputFrame`. Call sites: `NowInput.cs` 534, 647, 670, 705,
  783, 1009, 1055, 1067, 1077, 1486, 1526, 1570; `NowTextInput.cs` 110, 141, 223; `NowSurfaceToScreenMapper.cs:37`.
* `internal interface INowOverlayHost` — `isAlive`, `isActiveAndEnabled`, `CaptureProjection()`,
  `TryScreenPointToLocal(...)`, plus **`Component`-typed compatibility overloads that must be kept** so the Unity test
  assemblies keep compiling (this is the constraint that killed the M1 version of the refactor).
* `internal interface INowEventBufferedInputProvider` — a two-line marker on `NowUIToolkitInputProvider` and (in the Unity
  half) `NowIMGUIInputProvider`, with `NowOverlay.cs:516` testing the marker instead of the concrete type. Without it, an
  M2 browser provider that is not literally `NowUIToolkitInputProvider` loses retained popup footprints on idle frames
  (`NowOverlay.cs:508-527`, `RetainsFootprintWhileIdle`). This is the smallest, highest-value item on the list.
* `internal interface INowDeviceInputSource { bool TryRead(NowNavigationKeys keys, out NowMouseInput input); }` plus a
  settable static consulted at the top of `NowDeviceInput.Read` (~6 lines, inside a file whose readers are already fully
  define-fenced), so DOM pointer/wheel input can drive the unchanged `NowScreenInputProvider`.
* Retyping `INowFocusNavigationProxy.owningSelection` from `GameObject` to `object`, so a DOM element id can be a focus
  owner. The type is `internal` (`NowFocus.cs:6`) so there is no public API impact — but it is not free, and it buys
  nothing until M3 needs multi-surface focus.

---

## 5. Per-file plan for the 65 non-core files

Legend — **none**: compiles unchanged against the shim and the stand-ins. **guard**: an `#if` region inside the existing
file. **split**: the class becomes `partial`; members move **verbatim** into a new `X.Unity.cs` (whole-file
`#if !NOWUI_STANDALONE`) and a new `X.Standalone.cs` (whole-file `#if NOWUI_STANDALONE`) is added. **exclude**: not
compiled by the standalone csproj.

**Public API impact in Unity is `none` for every row.** Verbatim moves preserve accessibility; guards are inside private
native bindings or a private class; stand-ins and standalone halves do not exist in Unity.

### 5.1 Inventory B.2 — "core-with-guards" (31 files): all 31 are **none**

| File | What the inventory flagged | Why it compiles unchanged |
|---|---|---|
| `Runtime/NowFont.cs` | `NowWorldGraphic.ReleaseCachedMaterial` (2256); `NativeArray<byte>` (3730); `ScriptableObject` base; native compile-out | stand-in `NowWorldGraphic`; shim `NativeArray`; shim `ScriptableObject`; `NowTextShaper`'s existing `DllNotFoundException` probe; `NOWUI_VG_DISABLE_NATIVE` |
| `Extensions/Sdf/NowSdf.cs` | host call (3548); reset attribute (2528-2532); `Resources`/`Shader.Find` | stand-in; shim attribute; provider-backed `Resources`/`Shader.Find`; `HasProperty` answers via `NowShaderInfo` (`_NowSdfAbiVersion`, 3508) |
| `Runtime/Controls/NowFocus.cs` | `INowFocusNavigationProxy.owningSelection : GameObject`, `IsOwningProxySelection` (1508-1522); reset attribute | scene stub `GameObject`; fake-null `==` at 1515 works because the stub derives from `Object` |
| `Runtime/NowThemeAsset.cs` | `: ScriptableObject, ISerializationCallbackReceiver` (6-7); `OnEnable`/`OnValidate`/`OnAfterDeserialize` (396-417); ~140 attributes | shim base + interface + attributes; `CreateInstance` dispatches `OnEnable`; `OnValidate` is never dispatched (editor-only, matching a player) |
| `Runtime/Controls/NowOverlay.cs` | `Component`/`RectTransform`/`Camera` identity (208-227, 417-461, 508-527, 701-749, 1143-1156, 1440-1460, 1559-1664, 1753-1768); `NowRectTransformProjection`; `NowRaycastGate.IsHostAbove`; provider type tests (1605-1625) | scene stubs (host references are always null and each site tests `== null` first — verified at 427-431 and 516-527); stand-in `NowRectTransformProjection`; the real `NowIMGUIInputProvider` type now exists, so the `is` tests compile and evaluate false |
| `Runtime/NowMesh.cs` | `UploadMesh` (1956-2026); layout tables (185-226); `ToUnityBounds` (1944-1954); `QualitySettings.activeColorSpace` (1766-1786, 1826-1856) | shim `Mesh`/`VertexAttributeDescriptor`/`SubMeshDescriptor`/`Bounds`; `QualitySettings.activeColorSpace` **Gamma** so `PatchTextCanvasColors` early-returns exactly as in Unity |
| `Runtime/Controls/NowTextField.cs` | `TouchScreenKeyboard` (420-421, 1564-1573, 1697-1740); `UNITY_EDITOR` block (1286-1292) | shim (`isSupported == false`); `UNITY_EDITOR` undefined |
| `Runtime/Input/NowInput.cs` | 12 `is NowIMGUIInputProvider` sites; `Reset()` host calls (1304-1308); `NOWUI_INPUT_SYSTEM` regions | the **real** `NowIMGUIInputProvider` and `NowGUI` compile (H.4), so `Reset()` is untouched and side-effect-equivalent (TP R8); the Input System lines are already fenced |
| `Runtime/Controls/NowNumericExpression.cs` | `ExpressionEvaluator.Evaluate` (38-60) | shim returns `false` → the bounded fallback parser (TP R9 is the acceptance risk, not a build risk) |
| `Runtime/NowGradient.cs` | host release hook (712-715); reset attribute (1035-1040); `Gradient` | stand-in; shim attribute; shim `Gradient` with identity `GetHashCode`; `Material.mainTexture` getter identity (675/678) |
| `Runtime/Lottie/NowLottieRenderer.cs` | `NowLottieBurstTessellator` (51, 727-732, 802-806) | stand-in returns `false`/no-op → the existing scalar tessellator path |
| `Runtime/Controls/NowRichText.cs` | `Gradient` public API; dev warning (445-518) | shim; `DEVELOPMENT_BUILD` defined in Debug |
| `Runtime/Controls/NowRichTextParser.cs` | `Resources.Load<NowLottieAsset>` + `Time.time` (760-799) | shim |
| `Runtime/Lottie/NowLottieNative.cs` | `LIBRARY_NAME` selection (157-161) | the csproj defines the **existing** `NOWUI_VG_DISABLE_NATIVE` valve |
| `Runtime/Controls/NowComboBox.cs` | reset attribute (751-757) | shim attribute |
| `Runtime/Controls/NowTextInput.cs` | IMGUI fast path (137-181, 216-225); `Application.platform` static init (129) | the real `NowIMGUIInputProvider` compiles; `Application.platform` is readable before any backend exists (hazard D.1 #4) |
| `Runtime/NowDrawList.cs` | reset attribute (402-406); Destroy/`isPlaying` (225-252, 457-469) | shim; `isPlaying == false` in tests → the `DestroyImmediate` branch, as in EditMode |
| `Runtime/NowMaterialControlRenderer.cs` | `[CreateAssetMenu]` (5-6); SO base | shim |
| `Runtime/Controls/NowMaskField.cs` | `LayerMask` surface (116-127, 406-467, 487-490, 500-503) | shim `LayerMask` over `host.layerNames` with Unity's default table |
| `Runtime/NowManagedFontSession.cs` | Burst/Jobs/Collections/Mathematics (316-409, 421-439) | sequential Jobs shim + `NativeArray` + `UnsafeUtility.MemCpy` + `NativeArrayUnsafeUtility.GetUnsafePtr` |
| `Runtime/NowGlass.cs` | reset attribute (386-393); Resources templates (323-337) | shim |
| `Runtime/Input/NowInputSnapshot.cs` | `NowInputSurface.FromCamera(Camera)` (56-69) | scene stub `Camera` (always the null path) |
| `Extensions/Sdf/NowSdfImageAtlas.cs` | `SystemInfo.maxTextureSize` (137); RT helpers | shim; `IsCreated()` stays true on the null backend |
| `Runtime/NowFrame.cs` | `ProfilerMarker` (117, 157, 173) | shim `ProfilerMarker` with a public readonly `AutoScope` |
| `Runtime/NowTheme.cs` | `DefaultAsset()` `CreateInstance` (79-103); reset attribute | shim |
| `Extensions/Markup/NowMarkup.cs` | reset attribute (172-176); `File(path)` `System.IO` (102-116) | `System.IO` is plain .NET on desktop; browser degradation is M2's `INowFileSystem` |
| `Runtime/Input/NowSurfaceToScreenMapper.cs` | `is NowIMGUIInputProvider` (48); `Screen.height` | the real type compiles; shim `Screen` |
| `Runtime/Controls/NowEventSystemFocusBridge.cs` | already fully `#if NOWUI_UGUI` | compiles to its `false` stubs |
| `Runtime/NowFontFamily.cs` | attributes; SO base; no setters for standalone population | shim; the test/browser resource provider populates `_regular/_bold/_italic/_boldItalic/_fallbacks` by **reflection**, which is what `NowFontResolutionTests` already does (TP §3.3) |
| `Runtime/Controls/NowClipboard.cs` | default delegates over `GUIUtility.systemCopyBuffer` (14, 16) | shim `GUIUtility.systemCopyBuffer` is null-safe, so the static initialiser cannot throw |
| `Runtime/NowControlRenderer.cs` | `: ScriptableObject` + `CreateInstance` (5-22) | shim |

### 5.2 Inventory B.3 — "split-file" (12 files)

| File | Action | Exact regions, new files, and what the standalone half does |
|---|---|---|
| `Runtime/Now.cs` (4458) | **none** | 1121-1126 `Camera.current` → the scene stub is null → identity matrix; 1128-1207 `GL.*`, `Material.SetPass`, `Graphics.DrawMeshNow` → the shim immediate path; 1063/1087 → stand-in; 1098-1107 `DestroyCachedMaterial` → shim `Destroy`; 1231-1239 `LoadRequiredResource` → shim `Resources`; 1400-1403 `Screen` → shim; 1771-1964 `UploadCapturedMeshes` → shim `Mesh` (copying); 1998-2033 the `GL` block and 2049-2225 the legacy glass replay (`RenderTexture.GetTemporary/active`, `GL.Clear`, `CommandBuffer`, `Graphics.ExecuteCommandBuffer`) → the shim. **Zero edits in the largest public file.** |
| `Runtime/NowFontCompiler.cs` (1251) | **guard** (1 region, 3 lines) | Line 9 becomes `#if !NOWUI_STANDALONE` / `#define NOWUI_MSDF_NATIVE` / `#endif`. A conditional `#define` before the first token is legal C#, and the file's own header comment (lines 1-8) already describes this valve. Effect: the `DllImport` bindings become the existing `DllNotFoundException` stubs, so `TryCompile`/`DynamicSession` take the managed path (the `forceManagedCompiler || !forceNativeCompiler` test at 430), and `CreateFont`/`CreateColorFont` (1029-1103) compile against shim `Texture2D`/`Material`/`Instantiate`/`CreateInstance`, `TryCopyAtlas(NativeArray<byte>)` (610-646) against the shim `NativeArray`. |
| `Runtime/NowManagedFontBaker.cs` (273) | **none** | `NowSdfBakeJob` (36-124) compiles against the Jobs/Mathematics/Collections shims; `[BurstCompile]` is a no-op attribute; `Schedule(cellCount, 1).Complete()` runs `Execute(i)` sequentially. The job body **is** the port. |
| `Runtime/Input/NowDeviceInput.cs` (498) | **none** | 135-320 and 322-489 are already fully fenced by `NOWUI_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM` / `ENABLE_LEGACY_INPUT_MANAGER`; with neither defined, `Read` compiles to the `#else` at 127-130 (`input = default; return false;`). Browser input arrives through `INowInputProvider` in M2. |
| `Extensions/Sdf/NowSdfImageField.cs` (494) | **none** | `Graphics.Blit`, `RenderTexture.GetTemporary/ReleaseTemporary/active`, `SystemInfo`, `Shader.PropertyToID`, `Resources.Load`/`Shader.Find` are all shimmed; bakes become recorded blits on the null backend and `isValid` stays true because `Create()` succeeds. |
| `Runtime/Controls/NowRaycastGate.cs` (369) | **none** | Without `NOWUI_UGUI` the file is its `#else` stubs (329-351), which need only `Component` and `Vector2`. The gate always answers "allowed". |
| `Runtime/NowModelPreview.cs` (3840) | **exclude** | The whole file (`Now.Model`/`DrawModel`/`CanDrawModel`, `NowModel`, `NowModelPreview`, `NowModelPreviewDriver : MonoBehaviour`, `NowModelPreviewManager`). It is a scene-camera feature the browser cannot provide. The only referencing file under `Runtime`/`Extensions` is `NowPipelineGraphic.cs`, which is also excluded (verified by grep). The absence is recorded in the public-API delta. |
| `Runtime/Lottie/NowLottieAsset.cs` (606) | **split** | `public sealed partial class NowLottieAsset`. Move **verbatim** to `Runtime/Lottie/NowLottieAsset.Unity.cs`: `SetSourceFromUrl` (154-174), `LoadFromUrl` (180-183), `LoadFromUrlInternal` (185-223), `DownloadSourceBytes` (373-485), `RequestExceedsLimit` (487-494), taking `using System.Collections;` and `using UnityEngine.Networking;` with them. Core keeps the limit/policy statics (20-60, including `maxJsonDepth`/`maxJsonNodes`, which `NowLottieModel` reads — hazard D.1 #15), the data properties and composition cache, `SetSource` ×2, `ExtractSourceJson` (230-317, which calls `NowZipArchivePreflight`), the URL helpers (319-371, 496-518), the byte helpers (520-592) and `DestroyRuntimeAsset` (594-603). `NowLottieAsset.Standalone.cs` implements `LoadFromUrl` over `INowFetchProvider` + `INowFetchSink`, reusing the core `TryValidateRemoteUrl`/`TryResolveRedirect` per-hop policy and the sink's `OnData` byte cap, polled from `NowRuntime.onFrame`; with `host.fetch == null` it fails fast with `"remote Lottie loading requires an INowFetchProvider"`. **This is a pure move: no member body changes.** |
| `Runtime/Lottie/NowLottieCache.cs` (374) | **split** | `public static partial class NowLottieCache` + `sealed partial class Entry`. Move to `NowLottieCache.Unity.cs`: `Runner : MonoBehaviour` (33-39), the `Entry.coroutine`/`Entry.request` fields, `GetRunner` (274-286), `Load` (212-272), plus **three extracted private statics** — `StartLoad(Entry)` (the `GetRunner(); entry.coroutine = _runner.StartCoroutine(Load(entry));` pair), `AbortLoad(Entry)` (the `request.Abort()/Dispose()` lines from 136-151 and 344-345), `DestroyRunner()` (162-170). The three core call sites become `StartLoad(entry)`, `AbortLoad(entry)`, `DestroyRunner()`. **These three are extractions, not moves** — call them out for line-by-line review; everything else is verbatim. `NowLottieCache.Standalone.cs` implements the same three: `StartLoad` → the standalone `NowLottieAsset.LoadFromUrl`, storing the handle in a `partial Entry` field; `AbortLoad` → abort the handle; `DestroyRunner` → unsubscribe `Tick` from `NowRuntime.onFrame`. |
| `Extensions/Markdown/NowMarkdownImages.cs` (826) | **split** | `public static partial class NowMarkdownImages` + `sealed partial class Entry`. Move to `NowMarkdownImages.Unity.cs`: `Runner : MonoBehaviour` (43-49), the `Entry.request/downloadHandler/operation` fields, `GetRunner` (757-768), `StartDownload` (265-303), the `UnityWebRequest` parts of `CompleteDownload` (320-343, 380-384) and of `TryDecodeDownloadedTexture` (424-473). Core gains one **extracted** method: `FinishDownload(Entry entry, byte[] bytes, long status, string location, string error)` carrying the status/redirect/limit/decode logic that both halves share, plus `TryDecodeImageBytes(byte[], string, out Texture2D, out string)` over `ImageConversion.LoadImage`. **The `CompleteDownload` extraction is the second non-verbatim change** — review it line by line. `NowMarkdownImages.Standalone.cs` implements `StartDownload` over `INowFetchProvider` (the same byte cap through `OnData`, the same redirect policy) with `Tick` polling `isDone`; with no decoder the decode fails and the entry reaches `Failed`, which is the existing error path. |
| `Runtime/Controls/NowFilePicker.cs` (3901) | **split** | `public partial struct NowFilePicker` + `sealed partial class ThumbnailEntry` (the type is `public struct NowFilePicker` at `:40`; it already has an explicit static constructor at `:198`, so making it `partial` changes no initialisation semantics). Move to `Runtime/Controls/NowFilePicker.Thumbnails.Unity.cs`, verbatim: `ThumbnailEntry.request/operation` (89-90), `StartThumbnailRequest` (2184-2255), `PollThumbnailRequests`, `CompleteThumbnailRequest` (2378-2436). Everything else stays in core: `TryReadEncodedImageSize`, `CreateThumbnailTexture` (2438-2489, shim `Texture2D`/`Graphics.Blit`), the `UNITY_EDITOR` blocks (2568-2603, 2678-2692, already fenced), the `Application.quitting` registration (3406-3413, shim event), and all `NowFilePickerUtility`/`NowFilePickerUserFolders` members the 50 gate tests use. `NowFilePicker.Thumbnails.Standalone.cs`: `StartThumbnailRequest` marks the entry failed (a `file://` thumbnail has no browser equivalent; a desktop host can add an `INowThumbnailLoader` later) and `PollThumbnailRequests` is a no-op. |
| `Runtime/NowRemoteContent.cs` (520) | **guard** (2 regions) | Wrap `using UnityEngine.Networking;` (line 4) and the whole `NowBoundedDownloadHandler` class (lines 8-278, i.e. its doc comment through its closing brace) in `#if !NOWUI_STANDALONE`. `NowZipArchivePreflight` (284-519) **stays in the standalone build** — `NowLottieAsset.cs:247` calls `NowZipArchivePreflight.Validate`. The byte cap in the standalone path is enforced by `INowFetchSink.OnData` returning `false`, which is the same contract the handler implements. |

### 5.3 Inventory B.4 — "host-only-exclude" (22 files)

**20 × exclude:**
`Runtime/NowWorldGraphic.cs`, `Runtime/NowGraphic.cs`, `Runtime/Lottie/NowLottieBurstTessellator.cs`,
`Runtime/NowWorldGlassBackdrop.cs`, `Runtime/NowVisualElement.cs`, `Runtime/NowUGUINavigationProxy.cs`,
`Runtime/NowPipelineGraphic.cs`, `Runtime/Input/NowWorldInputProvider.cs`, `Runtime/Input/NowKeyInput.cs`,
`Runtime/Input/NowRectTransformInputProvider.cs`, `Runtime/URP/NowUniversalRendererFeature.cs`,
`Runtime/Controls/NowKeyBindingField.cs`, `Runtime/Lottie/NowLottieGraphic.cs`,
`Runtime/HDRP/NowHighDefinitionCustomPass.cs`, `Extensions/Markup/Editor/NowMarkupBindingsGenerator.cs`,
`Runtime/NowEditorRebuildQueue.cs`, `Runtime/NowBootstrap.cs`, `Runtime/NowLayoutGraphic.cs`,
`Runtime/NowWorldLayoutGraphic.cs`, `Runtime/NowPipelineLayoutGraphic.cs`.

**2 × compiled instead (H.4):**

| File | Why it compiles | What it needs |
|---|---|---|
| `Runtime/NowGUI.cs` (1022, `public static class NowGUI` at `:8`) | Its entire Unity surface is `Event.current`, `EventType.{Layout,Repaint}`, `FocusType.Passive`, `GUI.DrawTexture`, `GUILayoutUtility.GetRect`, `GUIUtility.GetControlID`, `ScaleMode.StretchToFill`, `Time.{frameCount,realtimeSinceStartup}`, `Application.isPlaying`, plus `Rect`/`Color`/`Vector2`/`RenderTexture`. No `UnityEditor` reference, no other `using`. | §3.8 IMGUI shim + the graphics handles the shim already has. Dormant while `Event.current == null`. |
| `Runtime/Input/NowIMGUIInputProvider.cs` (884, `public sealed class NowIMGUIInputProvider : INowInputProvider` at `:11`) | Adds `GUI.changed`, `GUIUtility.hotControl`, 8 more `EventType` members, 22 `KeyCode` members and the 13 `Event` instance members listed in §3.8. Every `NowKeyInput` reference is inside `#if NOWUI_INPUT_SYSTEM` (`:59-63, :122-146, :296-298, :404-408, :494-496, :544-546, :644-655, :796-799`), which is undefined. | The same IMGUI shim. `NowIMGUIInputProvider.instance` (a `static readonly` field at `:13`) constructs an inert provider whose `TryGetSnapshot` returns false. |

Compiling these two is what lets `NowInput.cs`, `NowTextInput.cs`, `NowSurfaceToScreenMapper.cs` and `NowOverlay.cs` stay
byte-identical, keeps `NowInput.Reset()` side-effect-equivalent (TP R8), and keeps the standalone public API free of
fake types. `NowKeyBindingField` and `NowKeyInput` remain absent from the standalone API — they are
`NOWUI_INPUT_SYSTEM`-only in Unity too, so the delta records them as "absent in both configurations without the define".

### 5.4 Compile-fix watch list

The `ShimGapProbe` counted **type-resolution** errors only (3342 bare → 1587 with the value types); member-level errors
appear once the types exist. Every row below is fixed **on the shim side**; a core edit outside the six planned regions is
a design finding to report, not a fix to make (§7.6).

| Symptom | Files | Fix |
|---|---|---|
| A missing member on a shim type (`Rect.Overlaps`, `Mathf.Max(params)`, `Color.grayscale`, `Texture.dimension`, `Mesh.GetSubMesh`, …) | anywhere | add the member per VT §n; the compiler is the checklist |
| `using Unity.Collections;` / `Unity.Jobs` / `Unity.Mathematics` / `Unity.Burst` | NowFont, NowFontCompiler, NowManagedFontSession, NowManagedFontBaker, NowValueControls | the shim declares those namespaces with public types, so the usings resolve |
| A stand-in's namespace or accessibility does not match | NowLottieRenderer → `NowUI.Internal` | copy the real declaration (§3.11) |
| `Application.platform` compared against `RuntimePlatform` members the shim lacks | NowFilePickerUserFolders (`WindowsEditor/WindowsPlayer/OSXEditor/OSXPlayer/LinuxEditor/LinuxPlayer`) | ship the complete `RuntimePlatform` enum |
| `ProfilerMarker.AutoScope` stored in a public struct field | NowEffects (870), NowFrame | `AutoScope` is a public readonly struct |
| `texture.LoadImage(bytes, markNonReadable)` extension-method form | NowMarkdownImages | `ImageConversion` static class with `this Texture2D` extension methods |
| `Texture2D` constructed with `mipChain: true` then `Apply(true, …)` | NowFont colour pages | the shim derives `mipmapCount` from the size; the null backend ignores mips |
| `[CallerFilePath]` embedding absolute paths | NowControls, NowLayout, NowInspector, NowSdf, NowFoldout | `PathMap` is already in `Directory.Build.props` |
| `stackalloc` into `Span<T>` in an extension project with `AllowUnsafeBlocks=false` | NowUnityEditorControlRenderer and friends | legal without `unsafe`; no action |
| `Span`, `ReadOnlySpan<char>`, `??=`, `[^1]`, `Dictionary.TryAdd`, `BigInteger` | Now, NowLine, NowTextArea, NowRichText, NowCodeEditor, NowJson, NowTrueType, NowNodeGraphEvaluator, NowNumericExpression | net9.0 supplies all of them; `LangVersion 9.0` covers the syntax |
| NOWUI001/NOWUI002 warnings | Runtime and extensions build clean today; only Tests has deliberate discards | no analyzer reference from Tests |

---

## 6. Lifecycle and reset

### 6.1 Time and frame contract (D5, H.9)

* `NowRuntime.BeginFrame()` is the **only** incrementer of `Time.frameCount`. It also sets
  `Time.realtimeSinceStartup`/`AsDouble` from `host.clock.realtimeSeconds - startSeconds`, `Time.time` = the same value
  (no pause or scale semantics in M1), `Time.deltaTime` = `now - previousFrameNow` (0 on the first frame, clamped to
  `>= 0`), resets the per-frame `GUIUtility.GetControlID` counter, calls `backend.BeginFrame(frameCount)` and raises
  `NowRuntime.onFrame` (which is where the standalone halves' `Tick`s run).
* `NowRuntime.EndFrame()` drains the destroy queue (§6.3), trims the temporary-RT pool (8-frame idle), and calls
  `backend.EndFrame()`.
* `Time.realtimeSinceStartup` is **live**: it reads the clock at call time, not once per frame. `NowViewStackTests` (×1)
  and `NowControlsTests` (×5) sleep 20–60 ms and compare, and would fail on a frame-latched clock (TP §4.2).
* When no frame has ever begun, `frameCount` is 0 and the clock still advances — the EditMode environment the suite was
  written for. In the **Tests host**, `frameCount` starts at **1** and an assembly-level `ITestAction.BeforeTest` calls
  `BeginFrame()` exactly once per test: constant *within* a test (which
  `NowControlsTests.ImmediateTabNavigationDoesNotWaitForUnityFrameCount` and every `_scopeStartedAt == Time.frameCount`
  guard require) but rolled over *between* tests, so a fixture that forgets a `Reset()` cannot alias into the next.
* `NowInputSnapshot`'s short constructors keep reading `Time`; hosts that want explicit frames use the 20-argument
  constructor, exactly as in Unity.

### 6.2 `NowRuntime.ResetAll()` (D7, H.15)

1. Collect every method carrying `[RuntimeInitializeOnLoadMethod]` in the registered assemblies. Registration is either
   explicit (`NowRuntime.RegisterAssembly`) or, if none was registered, a **one-time** scan of
   `AppDomain.CurrentDomain.GetAssemblies()` for assemblies that reference `NowUI.Engine`. The resulting `MethodInfo[]` is
   cached and ordered by `loadType` (Unity's order: `SubsystemRegistration`, `AfterAssembliesLoaded`,
   `BeforeSplashScreen`, `BeforeSceneLoad`, `AfterSceneLoad`) and then by declaring-type full name, for determinism.
2. Invoke each. Non-static or parameterised methods are logged at Warning and skipped.
3. Then the engine-side resets: `globals.Reset()`, release and empty the temporary-RT pool, `NowImmediate.Reset()`
   (matrix stack, active target, active pass), drop the lazily built `Texture2D.blackTexture`/`whiteTexture`/…, reset the
   `NullRenderBackend` counters, and clear the `GetControlID` counter.
   **Not reset:** `Shader.PropertyToID`'s intern table — core types cache ids in `static readonly` fields and Unity does
   not reset them across a domain reload either.
4. Ordering: the engine resets run **after** the core `ResetForRuntimeLoad` methods, so anything those methods destroy
   releases through a live backend.

The ~20 statics that have no reset today (`NowLine._bezierMaterial`, the `NowRipple` materials,
`NowMaskShader._propertyBlock`, `NowTextShaper.s_unsupported`, `NowFont.s_dynamicSessionUnsupported`,
`NowSdf._builtInMaterialTemplate`, …) are **not** addressed in M1: they are equally un-reset by a Unity domain reload, and
touching them would be a core edit. `ResetAll` is documented as "equivalent to a Unity domain reload", no better and no
worse. Making a mid-session backend swap sound is an M2 item (§11).

### 6.3 Destroy semantics

```
DestroyImmediate(obj[, allowDestroyingAssets])  -> destroy now
Destroy(obj)                                    -> Application.isPlaying && deferDestroyToEndOfFrame
                                                     ? queue (drained by NowRuntime.EndFrame)
                                                     : destroy now
Destroy(obj, t)                                 -> queue with deadline Time.time + t; drained when the deadline passes
```

Destroying runs `OnDestroyResources()` (backend release for `Texture2D`/`RenderTexture`/`Mesh`/`Material`;
`Shader`/`Sprite` have nothing to release), then for a `ScriptableObject` dispatches `OnDisable()` and `OnDestroy()`, then
sets `isDestroyed = true`. Fake-null (`== null`, `!obj`, ternary truthiness), `GetHashCode` stability,
`MissingReferenceException` on `name`/`hideFlags`, and `ToString() == "null"` all follow VT §13.

**Why deferral rather than "immediate is close enough":** in Unity, `Destroy` during play mode defers to end of frame, and
every core site chooses between the two on `Application.isPlaying` (hazard D.1 #2 lists 20 sites). With the Tests host at
`isPlaying = false`, every one of those sites takes `DestroyImmediate`, so the gate is unaffected either way — but the
browser runtime runs with `isPlaying = true`, and reproducing Unity there costs ~25 lines. `deferDestroyToEndOfFrame` is
kept as a debug switch for bisecting a suspected timing difference.

**The `??` / `?.` / `??=` bypass audit** (hazard D.1 #1 lists six sites; all were read):

| Site | Construct | Verdict |
|---|---|---|
| `NowRichTextParser.cs:773` | `Resources.Load<NowLottieAsset>($"Lottie/{id}") ?? Resources.Load<NowLottieAsset>(id)` | Safe. Both operands are freshly loaded (a provider never returns a destroyed object), and the very next statement is `if (asset == null) return false;`, which *is* fake-null aware. |
| `NowTextField.cs:1419` | `fontAsset?.TryResolveFont(NowFontStyle.Regular, out resolvedFont);` | Safe **and identical in Unity** — `?.` bypasses fake-null there too. Calling `TryResolveFont` on a destroyed root returns `false` with default metrics, which is exactly what `NowFontResolutionTests.DestroyedRootReturnsDefaults…` asserts. |
| `NowEffects.cs:870` | `public RenderTexture texture => entry?.target;` | Not a Unity object — `entry` is a plain managed class. No bypass. |
| `NowSdfImageAtlas.cs:309/315` | `_fieldAtlas ??= CreateTarget(...)`, `_colorAtlas ??= CreateTarget(...)` | Identical in Unity; and the immediately preceding lines call `ReleaseTexture(ref _colorAtlas)`, which nulls the field, so a destroyed handle never survives to the `??=`. |
| `NowSdfImageField.cs:404` | `field.color ??= CreateTarget(...)` | Same pattern; `Release(field)` on the line above nulls the handle. |
| `NowSdf.cs:5049` | `_texture ??= graph.texture;` | Safe — the next expression is `graph.texture != null && _texture != null`, a fake-null test. |

Conclusion: none of the six is a shim divergence. `??`/`?.`/`??=` bypass fake-null in Unity as well, so the shim
reproduces the behaviour by having the same operators. The only thing that could have differed was *when* an object
becomes destroyed, and §6.3 removes that difference.

### 6.4 Static-initialiser safety (hazard D.1 #4)

Every shim static reachable from a core static initialiser is backend-free: `Shader.PropertyToID` (a pure intern table),
`new MaterialPropertyBlock()` (a bag), `new ProfilerMarker(name)` (a string), `Application.platform` (a host field with a
default), `new Vector4[…]`, `Texture2D.blackTexture` (allocates a CPU store; the GPU upload is deferred to first bind),
`new CommandBuffer { name = … }` (a list), `new Mesh { … }` (no GPU), `new RenderTexture(...)` (no GPU until `Create()`),
`Time.frameCount` (a field), `Enum.GetValues(typeof(KeyCode))`. `NowRuntime`'s own static constructor installs
`DefaultHostServices` and `NullRenderBackend` and touches no core type, so there is no initialisation cycle.

**No unit in this plan adds a static constructor to an existing NowUI type.** Doing so would remove `beforefieldinit` and
insert a class-init check on every static access — a measurable cost on paths like `NowLottieRenderer`'s per-frame
tessellation pools, and therefore not behaviour-neutral under D1.

### 6.5 `ScriptableObject` messages (hazard D.1 #7)

`CreateInstance<T>` dispatches `Awake()` then `OnEnable()` — so `NowThemeAsset.OnEnable` invalidates its palette cache and
bumps `_contentVersion` exactly as it does in Unity. `Destroy`/`DestroyImmediate` dispatch `OnDisable()` (NowFont resets
its dynamic sessions) then `OnDestroy()` (NowFont clears the dynamic cache and disposes the shaper). `OnValidate` is never
dispatched, matching a player. A standalone loader that populates a `NowThemeAsset` or `NowFont` from exported data calls
`((ISerializationCallbackReceiver)asset).OnAfterDeserialize()` after setting the fields and before first use, reproducing
Unity's deserialize→enable order. The theme defaults built by `NowTheme.DefaultAsset()` are ordinary shim objects that
nothing destroys, so the source comment "never destroyed by NowUI" continues to hold.

### 6.6 `Application.quitting` and shutdown

`NowRuntime.Shutdown()` raises `Application.quitting` (which is where `NowFilePicker` releases its thumbnails), then calls
`ResetAll()`, then destroys every still-live `Object` the shim tracks in a weak list — releasing backend resources — and
finally restores the default host services and the null backend. Browser hosts call it from `beforeunload`/`pagehide`;
the Tests fixture calls it from `OneTimeTearDown`.

---

## 7. Verification plan for M1

### 7.1 Build (exit criterion D8(a))

```
dotnet build Standalone/NowUI.Standalone.sln -c Debug
dotnet build Standalone/NowUI.Standalone.sln -c Release
```

Zero errors in both. Warnings are counted, not gated (§1.2); the count goes into `Docs/Standalone/M1-Report.md`.

### 7.2 Tests (exit criterion D8(b))

```
dotnet test Standalone/NowUI.Engine.Tests/NowUI.Engine.Tests.csproj -c Debug
dotnet test Standalone/Tests/Tests.csproj -c Debug --filter "Category!=NowUI.Overview"
```

**Tests-local support** (`Standalone/Tests/`, all new):

* `Shims/LogAssert.cs` — `namespace UnityEngine.TestTools`,
  `public static class LogAssert { public static void Expect(LogType type, string message); public static void
  Expect(LogType type, Regex message); public static void NoUnexpectedReceived(); public static bool
  ignoreFailingMessages { get; set; } }`, backed by `NowRecordingLogger : INowLogger`. Expected entries are matched FIFO
  by exact string or regex; at teardown, an unconsumed `Error`/`Exception`/`Assert` entry **fails the test** — Unity's
  parity rule, and the mechanism that turns a missing font or material into a visible failure instead of blank geometry
  (TP §4.4, R7).
* `Shims/PerformanceTesting.cs` — `namespace Unity.PerformanceTesting`:
  `PerformanceAttribute : NUnit.Framework.CategoryAttribute` (deriving from `CategoryAttribute("Performance")` so
  `--filter Category!=Performance` works as in Unity); `Measure.Method(Action) → MethodMeasurement` with
  `SampleGroup(SampleGroup)`, `WarmupCount(int)`, `MeasurementCount(int)`, `IterationsPerMeasurement(int)`, `Run()`;
  `Measure.Custom(SampleGroup, double)`; `SampleGroup(string name, SampleUnit unit = SampleUnit.Millisecond,
  bool increaseIsBetter = false)`; `enum SampleUnit { Nanosecond, Microsecond, Millisecond, Second, Byte, Kilobyte,
  Megabyte, Gigabyte, Undefined }`.
* `Shims/Recorder.cs` — `namespace UnityEngine.Profiling`:
  `public sealed class Recorder { public static Recorder Get(string samplerName); public bool isValid => false;
  public bool enabled { get; set; } public void FilterToCurrentThread(); public void CollectFromAllThreads();
  public int sampleBlockCount => 0; }`.
  **These two files are what make `BenchmarkSupport/NowBenchmarkAllocations.cs` compile**, and four gate files
  (`NowMarkupTests`, `NowNodeGraphTests`, `NowNodeGraphIndexedEvaluationTests`, `NowTextStylingTests`) consume it.
* `Shims/AssemblyInfo.cs` — `[assembly: NonParallelizable]`, `[assembly: LevelOfParallelism(1)]` (every NowUI subsystem is
  a static singleton), and the assembly-level `ITestAction` that calls `NowRuntime.BeginFrame()` before each test.
* `Support/NowStandaloneTestHost.cs` — a root-namespace `[SetUpFixture]` whose `OneTimeSetUp`, in order:
  `NowRuntime.isPlaying = false;` (TP §4.3 — asserted by a self-test), `NowRuntime.colorSpace = ColorSpace.Gamma;`,
  `NowRuntime.Initialize(new NowStandaloneTestHostServices(), new NullRenderBackend());`, and whose `OneTimeTearDown`
  calls `NowRuntime.Shutdown()`.
* `Support/NowStandaloneTestResources.cs` — an `INowResourceProvider` that serves, from `Fixtures/materials.json` and
  `Fixtures/shaders.json`, the nine non-UGUI material templates (`NowUI/UIMaterial`, `TxtMaterial`, `TxtMaterialRGBA`,
  `GradientMaterial`, `GlassMaterial`, `GlassBlurMaterial`, `RippleMaterial`, `BezierMaterial`, plus the SDF templates)
  and the shaders reachable by `Shader.Find` (`NowUI/UI Bezier`, `NowUI/Color Picker`, `NowUI/UI Ripple`,
  `NowUI/SDF Scene`, `Hidden/NowUI/SDF Image Field`); and, in Wave 2, `NowUI/NotoSans` as a `NowFontFamily` built from
  `Fixtures/NowUI/*.font.json` — `CreateInstance<NowFont>()`, `name`, `atlas = new Texture2D(w, h, RGBA32, false)`
  (metadata only), `atlasInfo` deserialised with `System.Text.Json` (`IncludeFields = true`), `material` = a clone of the
  template — with `NowFontFamily._regular/_bold/_italic/_boldItalic/_fallbacks` and `NowFontAsset._fallbacks` set by
  reflection. It **must return the same instance on every `Resources.Load`** (`NowTextWrapTests` compares by reference and
  `Now.defaultFont` is reloaded after each `TearDown`) and **must not clone materials per draw**
  (`NowTextStylingTests.GradientTextDrawsRemainTextAndBatchTogether` expects one batch for two gradient draws).

**Wave 1 — smoke (no font):** the 12 font-free files, 165 cases. Acceptance: green.

**Wave 2 — the full gate:** 36 files, **783 cases, 9 expected `Assert.Ignore` skips** (6 in `NowTextShapingTests`, 3 in
`NowTextStylingTests` — HarfBuzz is compiled out per D6). CI must treat `Skipped` as a pass. Tier C
(`NowGraphEvaluationPerformanceTests`, 18 cases) compiles and can be run without the category filter as a CPU sanity
check, but is not part of the gate.

**What the null backend must provide for these tests** (TP §4.1 — the M1 subset never touches a device; every draw goes
through the capture path `NowDrawList → NowMesh.UploadMesh → shim Mesh setters`): `Mesh` channel storage with
`GetUVs`/`GetNormals` read-back; `Material` clone/`HasProperty`/`SetVectorArray`/`mainTexture` identity; `Texture2D`
managed pixel storage with the sub-rect `SetPixels32` and `Apply`; `Shader.PropertyToID` usable from static initialisers;
`Resources.Load<Material>` returning non-null for the nine templates; `Object.DestroyImmediate` with exact fake-null;
`ScriptableObject.CreateInstance<T>` for `Tests`-assembly subclasses; `ProfilerMarker.Auto()`;
`RenderTexture.Create()`/`IsCreated()` stable. Nothing must throw, and nothing may allocate on the steady-state path.

### 7.3 Unity harness (exit criterion D8(c))

1. **Baseline.** Already recorded: EditMode 1860 (1857 passed, 1 failed —
   `NowHarnessAnimationTests.ReadmeShowcasesHaveTheirDeclaredCaptureDurations`, a path-dependent check that fails in a
   worktree — 2 skipped) and PlayMode 163 (159 passed, 0 failed, 4 skipped), in
   `artifacts/local/standalone-baseline/`, produced from the worktree `D:/wkspaces/unity/nowui-base`.
2. **Checkpoint runs.** After U20 (all `Assets/NowUI` edits landed and `.meta` files imported) and again at the end:
   `pwsh -File Tools/NowUI-Harness.ps1 -Mode EditMode -ProjectPath <root> -ArtifactsPath artifacts/local/m1-after` and
   the same with `-Mode PlayMode`. Unity must be closed for the project under test.
3. **Comparison method — the gate is a set difference, not a count.** `Tools/Standalone/Compare-TestResults.ps1` parses
   both NUnit XMLs, joins on `test-case@fullname`, and emits three sets: cases whose `result` changed, cases present only
   in the baseline, cases present only in the after-run. **The gate is that all three sets are empty.** Absolute pass
   counts are deliberately not compared: this machine has known EditMode and Golden drift, so only the difference is
   meaningful. `Visual`/`Golden` modes are not part of the gate for the same reason.
4. **`.meta` import.** Before step 2's first run, `Unity.exe -batchmode -quit -projectPath <root> -logFile
   artifacts/local/meta-import.log` once, so the new files have committed `.meta` files and Unity's file set matches the
   baseline's for the right reason. The generated metas are **staged**, and the `git add`/`git commit` commands are handed
   to the user.

### 7.4 Public API and file-set proof (exit criterion D8(d))

* `Tools/Standalone/Dump-PublicApi.ps1 -Assembly <path> -Out <file>` reflects public types and members into a sorted,
  deterministic text file.
* **Unity gate:** dump `Library/ScriptAssemblies/NowUI.Runtime.dll` and the seven extension assemblies before and after;
  the diff must be empty.
* **Standalone delta:** dump the standalone `NowUI.Runtime.dll` and diff it against the Unity dump; the result is checked
  in as `Docs/Standalone/StandaloneApiDelta.md`. Expected content in M1: the `Now.Model`/`NowModel`/`NowModelPreview`
  family, and `NowKeyBindingField`/`NowKeyInput` (absent in Unity too without `NOWUI_INPUT_SYSTEM`). Any other entry is a
  finding.
* **File set:** `git status --porcelain Assets/NowUI` matches §2.7's expected list exactly; `git diff --stat --
  "Assets/**/*.asmdef"` is empty; `git diff -M --color-moved=zebra` shows the four splits as moves, with only the two
  named extractions (`NowLottieCache`'s three private statics and `NowMarkdownImages.FinishDownload`) as real edits.

### 7.5 Shim semantics suite (`NowUI.Engine.Tests`, NUnit 4)

Extends the existing 780 cases. Highest-value additions, each asserting the exact value or bit pattern from the specs:

| Area | Cases |
|---|---|
| **Gradient** (GC §3) | key-time quantisation (`0.5 → 0.5000076 [0x3F000080]`, `0.25`, `0.75`, `1/9`, exact `0.2`/`0.6`); out-of-range clamping; 0/9/10-key arrays silently ignored with the previous keys kept; a single key expanded to two at 0 and 1; stable sort with duplicate times; getters returning fresh arrays; the `u`-in-fixed-point Blend samples (`(0.2,0.4,0.6)@0 → (0.9,0.1,0.3)@1` at `t = 0.1` giving `(0.27 [0x3E8A3D71], 0.37 [0x3EBD70A4], 0.57000005 [0x3F11EB86])`; `k0 = 8520, k1 = 39976` at `t = 0.2` giving `u = 0.14582273 [0x3E15528E]`); nothing clamped for HDR/negative keys; Fixed stepping colour **and** alpha; `Evaluate(NaN) == (0,0,0,0)`; the PerceptualBlend byte table; content `Equals` with identity `GetHashCode` (an equal copy is **not** found in a `Dictionary`) |
| **AnimationCurve** (GC §5) | wrap-mode normalisation (`Once/Clamp/3/16/-1 → ClampForever`, read back as 8); `Default` evaluating as Loop (`Linear(0,0,1,1)`: `-0.25 → 0.75`, `1 → 0`, `1.25 → 0.25`, `3 → 0`); `ClampForever` at `±∞ → NaN`; PingPong samples; the Hermite bit-exact table (`EaseInOut(0,0,1,1)` at `0.3 → 0.21600002 [0x3E5D2F1C]`; the `(1,2,out 0.5)`/`(3,-1,in -2)` set; the 1e-7-segment case `5e-8 → 7.4975003e-7 [0x3549426E]`); infinite-tangent steps in both directions; weighted-Bézier samples at 1e-6; `SmoothTangents` table; `AddKey`/`MoveKey` duplicate rules; `this[-1]` → `IndexOutOfRangeException("GetKey")`; `sizeof(Keyframe) == 32`; `Equals`/`GetHashCode` asymmetry over wrap modes and `tangentMode`; empty-curve hash 0 |
| **ColorUtility** (GC §6) | `#f00`/`#f008` digit doubling; 8-digit alpha; whitespace trimming; case-insensitive names; `gray`/`pink`/`clear`/`light blue` failing while `grey` and `transparent` succeed; **failure yielding `Color.white`**; `ToHtmlStringRGB((1,0.5,0.25,0.75)) == "FF8040"` and the RGBA form; non-finite → `"00"` |
| **LayerMask** (GC §7) | `LayerToName` for `-1, 3, 6..31, 32, 100, int.MinValue` → `""` without throwing; `NameToLayer("")`/`(null) == 3`; `GetMask("Default","UI") == 33`; `GetMask(new string[]{null}) == 8`; `GetMask(null)` → `ArgumentNullException("layerNames")` |
| **Object fake-null** (VT §13) | `destroyed == null` true, `(object)destroyed == null` false, `(bool)destroyed` false, `destroyedA == destroyedB` false, `obj?.name` throwing `MissingReferenceException`, `obj ?? fallback` returning the destroyed object, hash unchanged, `ToString() == "null"`, `Destroy(null)` silent, double destroy silent; **deferred `Destroy` under `isPlaying = true` becoming visible only after `EndFrame`**, and immediate under `isPlaying = false`; `CreateInstance` → `OnEnable`, `Destroy` → `OnDisable` then `OnDestroy`, on a recording subclass |
| **Material / Mesh / Texture2D / NativeArray** | `SetVectorArray` copying and preserving length, reusing the stored array when lengths match (allocation assertion); `CopyPropertiesFromMaterial` deep-copying arrays; `mainTexture` getter identity; `HasProperty` true for declared-but-unset shader properties; `SetUVs`/`GetUVs` round trip with list-clearing semantics; interleaved `SetVertexBufferData` + `GetVertices` de-interleaving; `SetIndexBufferData<ushort>` + `GetTriangles`; `Texture2D.GetRawTextureData<Color32>()` aliasing `GetRawTextureData<byte>()`; `Apply` bumping `updateCount`; bottom-up `SetPixels32(x,y,w,h)` row placement; the full `NativeArray.Copy` overload matrix; `GetUnsafePtr` writes visible through the indexer |
| **Immediate path** | temporary-RT pool keying (a filter-mode change must **not** fragment the pool) and 8-frame trim; `CommandBuffer` replay order against `NowRecordingRenderBackend`; `GetTemporaryRT`/`ReleaseTemporaryRT` name resolution; unreleased temporaries freed at end of execution; `Blit` leaving the destination bound; `SetRenderTarget` always followed by `SetViewport`; `Graphics.ExecuteCommandBuffer` restoring the previous target |
| **Allocation** | a steady-state frame (`Clear` → `Set*` ×N → read-back) allocating **0 bytes** measured with `GC.GetAllocatedBytesForCurrentThread`, with the `NullRenderBackend` draw ring off |

### 7.6 Compile-driven bring-up (the process rule)

1. `dotnet build Standalone/NowUI.Engine` green, with the semantics suite compiling.
2. `dotnet build Standalone/NowUI.Runtime` — iterate on the shim until the error list is empty. **Every error must name
   either a missing Unity member (add it to the shim, per spec) or one of the six planned shared-file edits (do it). No
   other core edit is allowed: if the compiler demands one, it is a design finding to report, not a fix to make.** That
   rule is what keeps the two-guard/four-split promise honest across 130k LOC of member-level surprises.
3. Re-run `ShimGapProbe`'s method after each shim unit and record the error count; when it stops falling faster than
   units land, the remaining errors are member-level and step 2's loop is the whole job.
4. Extensions, then Tests, then `dotnet test`.

---

## 8. Work breakdown

Sized so a fleet of agents can implement the shim units in parallel (no two parallel units share a file), then a single
compile-error-driven loop fixes the residue, then the test waves run. **Start point: the value-type layer is done.**
The Unity tree compiles after **every** unit, because every shared-file change is either a whole-file `#if`-guarded new
file, a 3-line conditional `#define`, a region guard, or a verbatim member move.

| # | Title | Files created / edited | Depends on | Parallel-safe with | Acceptance check | LOC |
|---|---|---|---|---|---|---|
| **U0** | Solution skeleton | `Standalone/NowUI.Runtime/NowUI.Runtime.csproj`, 7 extension csprojs, `Standalone/Tests/Tests.csproj`, `NowUI.Standalone.sln`; `Tools/Standalone/Compare-TestResults.ps1`, `Tools/Standalone/Dump-PublicApi.ps1` | — | — | `dotnet restore` succeeds on the sln; a throwaway discard proves the analyzer loads and reports NOWUI001 (else switch to the `Analyzers~` project reference) | 300 |
| **U1** | Enums | `Engine/Enums/{HideFlags,KeyCode,RuntimePlatform,TextureEnums,DataEnums,ImguiEnums,Rendering}.cs` | U0 | U2–U9, U12–U19 | `dotnet build NowUI.Engine`; a test asserts 12 sampled numeric values against §3.2 | 500 |
| **U2** | Attributes | `Engine/Attributes/Attributes.cs` | U1 (`RuntimeInitializeLoadType`) | U3–U9, U12–U19 | builds; a reflection test reads `tooltip`/`header`/`min`/`max`/`minLines` back | 120 |
| **U3** | Object model, scene stubs, host interfaces, `NowRuntime` skeleton | `Engine/Object/{Object,ScriptableObject,Exceptions}.cs`, `Engine/Scene/SceneStubs.cs`, `Engine/Host/{INowHostServices,INowFetch,DefaultHostServices}.cs`, `Engine/NowRuntime.cs` | U1 (HideFlags, LogType, RuntimePlatform) | U4–U9, U12–U19 | the VT §13 fake-null table passes; `CreateInstance<T>` dispatches `OnEnable` on a `Tests`-assembly-style subclass | 700 |
| **U4** | Value-type top-ups | `Engine/Math/BoundsInt.cs`, `Engine/Math/LayerMask.cs` | U3 (`NowRuntime.host`) | U1, U2, U5–U9, U12–U19 | the GC §7 LayerMask table passes | 250 |
| **U5** | Services | `Engine/Services/{Time,Screen,Application,Debug,SystemInfo,QualitySettings,Resources,TouchScreenKeyboard,ExpressionEvaluator,ColorUtility}.cs` | U3 | U6–U9, U12–U19 | the GC §6 ColorUtility table passes (including white-on-failure); `Debug.LogException` formats `"Type: message"` | 550 |
| **U6** | Shader, Material, bags, globals | `Engine/Graphics/{Shader,Material,MaterialPropertyBlock}.cs`, `Engine/{NowMaterialBag,NowShaderGlobals,NowShaderInfo}.cs` | U3, U5 | U7, U8, U12–U19 | `SetVectorArray` copy/length and allocation tests; `HasProperty` over `NowShaderInfo` | 650 |
| **U7** | Textures and render targets | `Engine/Graphics/{Texture,Texture2D,RenderTexture,RenderTextureDescriptor,NowTemporaryRenderTexturePool,Sprite,ImageConversion}.cs` | U3, U6, U9 (`NativeArray` view) | U8, U12–U19 | round-trip and aliasing tests; pool keying and 8-frame trim | 900 |
| **U8** | Mesh and rendering structs | `Engine/Graphics/Mesh.cs`, `Engine/NowMeshData.cs`, `Engine/Graphics/Rendering/{SubMeshDescriptor,VertexAttributeDescriptor,RenderTargetIdentifier}.cs` | U1, U3 | U6, U7, U12–U19 | set/get round trips for all 8 UV channels; interleaved de-interleave; zero-allocation steady state | 600 |
| **U9** | Collections, Jobs, Burst, Mathematics, Profiling | `Engine/Collections/{NativeArray,UnsafeUtility}.cs`, `Engine/Jobs/Jobs.cs`, `Engine/Burst/Burst.cs`, `Engine/Mathematics/Mathematics.cs`, `Engine/Profiling/ProfilerMarker.cs` | U0 | U1–U8, U12–U19 | the `Copy` overload matrix; a sequential `IJobParallelFor` runs `Execute(i)` for all `i`; `GetUnsafePtr` writes visible through the indexer | 700 |
| **U10** | Backend contract, null and recording backends | `Engine/Backend/{INowRenderBackend,NowRenderCaps,NullRenderBackend,NowRecordingRenderBackend}.cs` | U6, U7, U8 | U11, U12–U19 | the null backend accepts a synthetic frame, allocates 0 bytes with the ring off, and validates null arguments | 550 |
| **U11** | Immediate path and CommandBuffer | `Engine/Graphics/{NowImmediate,GL,Graphics}.cs`, `Engine/Graphics/Rendering/{CommandBuffer,NowCommandOp}.cs` | U10 | U12–U19 | replay order and temp-RT resolution against the recording backend; the two invariants (viewport after target, view-projection before draw) asserted | 800 |
| **U12** | Gradient and AnimationCurve | `Engine/Data/{GradientKeys,Gradient,Keyframe,AnimationCurve}.cs` | U1, U4 | U1–U11, U13–U19 | the full GC §3/§5 fixture tables pass, including the bit patterns | 550 |
| **U13** | IMGUI shim | `Engine/IMGUI/Imgui.cs` | U1, U5, U7 | U6, U8–U12, U14–U19 | `NowGUI.cs` and `NowIMGUIInputProvider.cs` compile against it in a scratch project | 180 |
| **U14** | Runtime stand-ins | `Assets/NowUI/Runtime/Standalone/{NowWorldGraphic,NowLottieBurstTessellator,NowRectTransformProjection}.Standalone.cs` | U0 | U1–U13, U15–U19 | Unity EditMode still compiles (files are empty for Unity); signatures byte-match the real declarations | 120 |
| **U15** | Guards | `Assets/NowUI/Runtime/NowFontCompiler.cs` (3 lines at :9), `Assets/NowUI/Runtime/NowRemoteContent.cs` (2 regions: :4 and :8-278) | U0 | U1–U14, U16–U19 | `git diff` shows exactly 3 added `#if`/`#endif` pairs and no moved code; Unity EditMode unchanged | 10 |
| **U16** | `NowLottieAsset` split | `NowLottieAsset.cs` (→ `partial`), `NowLottieAsset.Unity.cs` (250 moved), `NowLottieAsset.Standalone.cs` | U3 (fetch interfaces) | U14, U15, U17-independent parts, U18, U19 | `git diff -M --color-moved` shows a pure move; Unity EditMode unchanged | 250 moved + 150 |
| **U17** | `NowLottieCache` split | `NowLottieCache.cs` (3 call sites), `NowLottieCache.Unity.cs` (110 moved + 3 extracted statics), `NowLottieCache.Standalone.cs` | U16 | U14, U15, U18, U19 | move review plus a line-by-line read of the three extracted statics; Unity EditMode unchanged | 110 + 80 |
| **U18** | `NowMarkdownImages` split | `NowMarkdownImages.cs` (→ `partial`, `FinishDownload`/`TryDecodeImageBytes` extracted), `NowMarkdownImages.Unity.cs` (160 moved), `NowMarkdownImages.Standalone.cs` | U3 | U14–U17, U19 | move review plus a line-by-line read of the `CompleteDownload` extraction; Unity EditMode unchanged | 160 + 140 |
| **U19** | `NowFilePicker` thumbnails split | `NowFilePicker.cs` (→ `partial` struct + nested `partial`), `NowFilePicker.Thumbnails.Unity.cs` (200 moved), `NowFilePicker.Thumbnails.Standalone.cs` | U0 | U14–U18 | pure move; Unity EditMode unchanged | 200 + 30 |
| **U20** | `.meta` import + Unity checkpoint | none (runs Unity once, then the harness) | U14–U19 | — | `Compare-TestResults.ps1` reports an empty difference against the baseline for EditMode and PlayMode; `.meta` files staged and the `git` commands handed to the user | 0 |
| **U21** | Runtime compile-green loop | `Standalone/NowUI.Engine/**` only | U1–U19 | — | `dotnet build Standalone/NowUI.Runtime` clean; a written list of every shim member added, and of any core edit the compiler demanded (which is a finding, not a fix) | 350 scattered |
| **U22** | Extensions compile-green | the 7 extension csprojs; shim additions | U21 | — | `dotnet build` of all seven clean | 120 |
| **U23** | Tests project and local shims | `Standalone/Tests/Shims/{LogAssert,PerformanceTesting,Recorder,AssemblyInfo}.cs`, `Support/{NowStandaloneTestHost,NowRecordingLogger}.cs`, the Wave 1 compile list | U22 | U24 | `dotnet build Tests` clean including `NowBenchmarkAllocations.cs`; a self-test asserts `Application.isPlaying == false` and `Time.frameCount == 1` in the first test | 450 |
| **U24** | Editor asset exporter | `Assets/NowUI/Editor/NowStandaloneAssetExport.cs` | U0 | U21–U23 | run once via `-executeMethod`; produces `NotoSans.family.json`, four `*.font.json`, `materials.json`, `shaders.json`; Editor asmdef only, so no runtime effect | 300 |
| **U25** | Test resource provider + fixtures | `Standalone/Tests/Support/NowStandaloneTestResources.cs`, `Standalone/Tests/Fixtures/**` | U23, U24 | — | `Resources.Load<NowFontAsset>("NowUI/NotoSans")` returns the same instance twice and resolves a Regular face with real metrics; all nine material templates non-null | 350 |
| **U26** | Wave 1 run and triage | `Tests.csproj` list; shim fixes | U23 | U24, U25 | 165 cases green; the log policy fires on an unexpected error | 60 |
| **U27** | Wave 2 run and triage | `Tests.csproj` list; shim fixes | U25, U26 | — | **783 cases, 9 skipped, 0 failed**; the four allocation cases pass, or the lead signs off on a `[Category("Allocation")]` opt-out (§1.2); TP R9 confirmed for all 61 `NowNumericExpression` cases | 200 |
| **U28** | Shim semantics suite | `Standalone/NowUI.Engine.Tests/**` | U4–U12 | U13–U27 (runs throughout) | every table in §7.5 green | 900 |
| **U29** | Public API dump and delta | `Tools/Standalone/Dump-PublicApi.ps1` output; `Docs/Standalone/StandaloneApiDelta.md` | U21, U22, U20 | U26–U28 | empty Unity-side diff; the standalone delta contains only the expected entries | 100 |
| **U30** | Final Unity harness comparison | none | U20, U29 | — | empty difference again after all work has landed | 0 |
| **U31** | Documentation and M1 report | `Docs/Standalone/README.md`, `Docs/Standalone/M1-Report.md` | U27, U29, U30 | — | build/test instructions, the exclude list, the three stand-ins, warning counts, case counts, the harness diff, and the open items | 200 |

**Parallelisation.** After U0: group **A** = U1, U2, U3, U9, U12, U14, U15, U19 (fully disjoint files, all can start at
once); group **B** = U4, U5, U6, U8, U13 once U3 lands, plus U16 (needs U3's interfaces) and U18; group **C** = U7 (needs
U6 + U9), U17 (needs U16), U10; group **D** = U11; then the serial tail U20 → U21 → U22 → U23 → {U24, U26} → U25 → U27 →
U29 → U30 → U31, with U28 running in parallel from group B onward.

**Critical path:** U0 → U3 → U6 → U7 → U10 → U11 → U21 → U22 → U23 → U25 → U27 → U29 → U30.

**Totals:** ≈ 9,400 new LOC (≈ 7,100 shim, ≈ 900 test support and fixtures, ≈ 900 semantics suite, ≈ 500 tooling), plus
≈ 720 lines moved verbatim inside `Assets/NowUI`. The reviewable diff under `Assets/NowUI` is ≈ 1,050 lines, of which
≈ 720 are verbatim moves and ≈ 300 are new files that are empty for Unity. **Real edits to existing Unity-compiled code
total about 25 lines:** 3 guard regions (6 `#if`/`#endif` lines), 7 `partial` keywords added to type declarations
(4 outer types + 3 nested), 6 rewritten call sites in `NowLottieCache`/`NowMarkdownImages`, and the two extracted method
bodies flagged in §5.2 for line-by-line review.

---

## 9. Risks and mitigations

| # | Risk | Likelihood / impact | Mitigation |
|---|---|---|---|
| 1 | **Member-level compile residue** in 130k LOC once the types resolve (the probe stopped at type resolution: 1587 diagnostics remained with only the value types present). | High / medium — cost, not design | U21 is budgeted for it and the compiler drives the work. The shim may grow any Unity member; core edits are forbidden, so the residue is visible in the U21 report rather than smeared into the sources. |
| 2 | **TP R9 — `NowNumericExpression` parity.** In Unity some of the 61 golden values come from `UnityEngine.ExpressionEvaluator`; standalone uses the fallback for all of them. | Medium / medium | GC §8 specifies the evaluator completely (grammar, precedence, `pi`, `f`/`d`/`l` suffixes, the 15-significant-digit Mono formatting, the cast rules), so a port is a known ~300 LOC job rather than research. Confirm in U27 before deciding. |
| 3 | **The four allocation-zero cases turn strict** under .NET 9's exact `GC.GetAllocatedBytesForCurrentThread`. | Medium / medium | The allocation rule is designed into `Mesh`, `NowMaterialBag`, `Texture2D` and `NullRenderBackend` (§1.2) and asserted directly in the semantics suite (§7.5), so a regression is caught before U27 rather than in it. |
| 4 | **Font fixture is the critical path** — 31 of 36 gate files reach `Now.defaultFont`, and the export needs a Unity run. | Certain / high for Wave 2 | U24 is scheduled early and in parallel; Wave 1 (165 cases) is a real gate that does not need it; the export is the prebaked `atlasInfo`, so metrics are Unity's by construction. |
| 5 | **Stand-ins are named after host types.** | Low / low | Only three remain, all `internal` (compiling the two IMGUI files removed both public ones). Each carries a header comment; `Docs/Standalone/README.md` lists them; §4.9 specifies the refactor that deletes them. |
| 6 | **The `Unity.Jobs`/`Unity.Mathematics` namespace shim** may read as re-litigating D6. | Medium / low | It is framed as "the scheduler is the shim, the kernel is unchanged" — no Burst/Jobs/Collections/Mathematics packages are referenced, execution is sequential and managed, and native plugins are compiled out. The hand-ported `BakeCell` alternative is a one-day swap touching two files and changes no other decision. |
| 7 | **`.meta` churn** for 11 new files under `Assets/NowUI`. | Certain / low | U20 imports once with `-quit`, stages the metas, and hands the commands to the user (commits are the user's call). |
| 8 | **Harness drift on this machine** (known EditMode and Golden noise at clean HEAD). | Certain / low | The gate is the set *difference*, never absolute counts; baseline and after-runs happen on the same machine, and Visual/Golden are excluded. |
| 9 | **Analyzer DLL fails to load** under the .NET 9 SDK, silently dropping NOWUI001/002. | Low / low | U0 proves it with a throwaway discard; the fallback is an `OutputItemType="Analyzer"` project reference to `Analyzers~/NowUI.Analyzers`. |
| 10 | **`Destroy` deferral** introduces a timing difference no test covers. | Low / low | With `isPlaying = false` the gate never exercises deferral; `deferDestroyToEndOfFrame` bisects any suspicion; the six bypass sites were audited (§6.3). |
| 11 | **`Mesh` copy cost** — the shim copies vertex streams on every upload. | Certain / low | Unity copies too; the buffers are pooled and grow monotonically. Measured in the perf harness later, not an M1 gate. |
| 12 | **Static-init cycle** between `NowRuntime` and a core static. | Low / medium | `NowRuntime`'s static constructor installs the defaults and touches no core type; the Tests fixture installs the host before any fixture runs; §6.4 enumerates every shim static reachable from a core initialiser. |
| 13 | **wasm-specific breakage in M2** — the `DllImport`s in `NowTextShaper`/`NowFontCompiler` are link errors rather than exceptions, and reflection-based `ResetAll` is trimmed away. | Certain in M2 / none in M1 | Both fixes are already planned (`NOWUI_STANDALONE_WASM` around the two `DllImport` groups and the three `LIBRARY_NAME` sites; explicit `RegisterAssembly` plus a linker descriptor). Neither undoes M1 work. |
| 14 | **Culture** — ICU stays on, so `NowNewControlsTests` calendar geometry follows the machine's culture. | Low / low | The test and `NowDatePicker` read the same `CultureInfo`, so they agree under any culture. Do not change it for tests alone (TP R12). |
| 15 | **PerceptualBlend red-channel deviation** (~2–5e-4 relative, up to ±1 byte). | Certain / very low | Documented in GC §3.6; NowUI never sets `PerceptualBlend`; the fixtures assert the byte table with a ±1 tolerance on R. |

**What to cut first if M1 must ship sooner**, in order — nothing on this list creates M2 or M3 rework, because each cut
removes a deliverable, not a decision:

1. **Wave 2 and the Editor exporter (U24, U25, U27).** Ship Wave 1 (165 cases) as the `dotnet test` gate and record the
   font work as the first M1.1 item.
2. **The `INowFetchProvider` implementations in the four standalone halves.** Ship the fail-fast bodies (~30 LOC each);
   the interfaces stay, so M2 adds only implementation.
3. **The `NowFilePicker` thumbnail split (U19).** Instead exclude `NowFilePicker.cs` and `NowFilePickerUserFolders.cs`
   from the Runtime csproj and drop their two test files (−50 cases). Revisit later with no other change.
4. **`Gradient.PerceptualBlend` and weighted `AnimationCurve` segments.** Leave a `NotSupportedException` with a clear
   message; Blend, Fixed and unweighted Hermite stay. NowUI itself never sets either mode.
5. **Breadth of the semantics suite (U28).** Keep the Gradient quantisation, the Hermite table, the ColorUtility table,
   the fake-null table and the allocation assertions; defer the rest.
6. **`NowRecordingRenderBackend` and the CommandBuffer replay tests.** The null backend's counters suffice for M1 — but
   note this is the one cut that costs M2 its cheapest correctness harness.

---

## 10. Decision log

| # | Question | Final answer (one line) |
|---|---|---|
| H.1 | Ship `ScriptableObject`? | Yes — shim `Object` + `ScriptableObject` + `ISerializationCallbackReceiver` + attributes; `CreateInstance` is `Activator`-based and dispatches `Awake`/`OnEnable`. |
| H.2 | Now.cs screen path | Shim emulation of `GL`/`Graphics`/`RenderTexture`/`CommandBuffer` over one immediate backend; `Now.cs` compiles unchanged. |
| H.3 | Font pipeline | Sequential `Unity.Jobs` shim runs `NowSdfBakeJob.Execute` unchanged; `NowFontCompiler.cs:9` guarded; `NOWUI_VG_DISABLE_NATIVE` defined; `NowTextShaper` untouched. |
| H.4 | Host classes referenced from core | Scene stubs with internal constructors + compile `NowGUI.cs` and `NowIMGUIInputProvider.cs` whole against an inert IMGUI shim + three internal stand-ins. No interface refactor in M1 (it breaks the Unity test assemblies). |
| H.5 | Assembly shape | One `NowUI.Runtime`; four `partial` splits (`NowLottieAsset`, `NowLottieCache`, `NowMarkdownImages`, `NowFilePicker`); `NowModelPreview.cs` excluded; 21-file exclude list in the csproj. |
| H.6 | Gradient / AnimationCurve | Implement `GradientCurveSemantics.md` exactly (16-bit quantisation, whole-array rejection, Default = Loop, ±∞ = NaN, `dx >= 1e-4`, white-on-failure `ColorUtility`); **no new probe** — that document is the probe. |
| H.7 | Asset format | One Unity Editor exporter → JSON: `NotoSans.family.json` + per-face `*.font.json` (prebaked `atlasInfo`), `materials.json`, `shaders.json`. |
| H.8 | WebGL2 v1 scope | M2; capability-gated (glass, SDF float formats, MSAA); ModelPreview and FilePicker excluded. |
| H.9 | Clock / frame | `NowRuntime.BeginFrame()` is the only `frameCount` incrementer; `realtimeSinceStartup` is live; the Tests host starts at 1 and ticks once per test. |
| H.10 | Coordinates | Unity's conventions kept; bottom-left `safeArea`; bit-exact `Ortho`; the backend owns the framebuffer flip. |
| H.11 | `DEVELOPMENT_BUILD` | Defined in the Debug configuration of every standalone project; the gate runs in Debug. |
| H.12 | Browser IO | Deferred to M2; `INowFetchProvider`/`INowFetchSink`/`INowFetchHandle` (streaming) declared now so the standalone halves are final. |
| H.13 | `NativeArray<T>` | Shimmed over a pinned `byte[]`, with `NativeArrayUnsafeUtility` and `UnsafeUtility`; no signature changes anywhere. |
| H.14 | `activeColorSpace` | Host-settable `NowRuntime.colorSpace`, **default Gamma** (`ProjectSettings.asset:49`); gate-observable through `NowTextStylingTests`' Canvas draw list. |
| H.15 | Reset | `[RuntimeInitializeOnLoadMethod]` is a no-op attribute; `NowRuntime.ResetAll()` reflects once, caches, and invokes in Unity's load-type order. |
| H.16 | Tests | `Standalone/Tests`, `AssemblyName=Tests`, NUnit 3.14.0, TP §3.1's list; Wave 1 = 165 cases, gate = **36 files / 783 cases / 9 skips**; Tier C filtered out. |
| H.17 | Culture | ICU everywhere — `InvariantGlobalization` is not set for runtime or tests (it is not set today either). |
| H.18 | Native ABI | Sequential float/int layouts with `[Serializable]`; already true of the implemented value-type layer. |

---

## 11. Deferred to M2 / M3

**M2 (WebGL2 backend and the browser host)**

1. `NowUI.Backend.WebGL2` implementing `INowRenderBackend` (§4.7), and `[assembly: InternalsVisibleTo("NowUI.Backend.WebGL2")]`
   added to `Standalone/NowUI.Engine/NowUI.Engine.csproj` — a standalone-only file, so still not a core edit.
2. **Per-pass render state.** `shaders.json` gains `blend`, `zwrite`, `ztest` and `cull` parsed from the `.shader`
   sources. Until then the backend hard-codes premultiplied-alpha blend, depth off, cull off — what all nine core programs
   use. This must be decided before the first GLSL port.
3. **GLSL ports** of the nine core programs (`UI Rectangle`, `Text Renderer`, `Text Renderer RGBA`, `UI Gradient`,
   `UI Glass`, `Hidden/GlassBlur` passes 0-3, `UI Ripple`, `UI Bezier`, `Color Picker`) plus the optional SDF programs,
   with the `NowUIColorSpace.cginc` behaviour keyed off `caps.colorSpace`.
4. **Async image decode:** the browser fetch provider **pre-decodes to RGBA** before bytes reach the shim, so
   `ImageConversion.LoadImage` stays synchronous and `NowMarkdownImages` needs no further split.
5. `INowFileSystem` (an in-memory or origin-private filesystem) for `NowFilePicker`, `NowFilePickerUserFolders`,
   `NowMarkupFile` and `NowMarkup.File`; async clipboard over the paste-event cache.
6. `NOWUI_STANDALONE_WASM`: the `"__Internal"` `LIBRARY_NAME` branch plus compiling out the `DllImport` groups in
   `NowTextShaper` and `NowFontCompiler` (link errors, not exceptions, on wasm), and `NowLottieNative`.
7. Trimming and AOT: explicit `NowRuntime.RegisterAssembly` calls plus a linker descriptor to replace the reflection scan
   in `ResetAll`, and to keep `NowInspector`'s field reflection alive.
8. **Context-loss recovery** as a supported flow: `IsRenderTextureLost` already exists; what remains is deciding whether
   `ResetAll` must survive a lost context, which requires resetting the ~20 statics that have no reset today.
9. **SDF bake budget:** measure `NowSdfBakeJob` inside a `requestAnimationFrame` budget and decide whether the browser
   needs time-slicing (the synchronous scheduler stays correct either way).
10. The `NowRecordingRenderBackend` op-log diff as the WebGL2 conformance harness.
11. Font atlas pixels and `_fontBytes` added to the export format (the fields are already reserved), enabling the managed
    baker in the browser.
12. Warnings ratchet: `TreatWarningsAsErrors=true` with an explicit `NoWarn`, starting from the count U31 records.

**M3 (JS mirror)**

13. The command-stream ABI: opcode numbering and versioning, string interning and lifetime, how `using (Now.StartUI())`
    and `[NowScope]` scopes are encoded, and — the hard part — how the previous-frame result table is keyed when the JS
    call sequence changes between frames (which is the normal case for immediate mode). This is the largest M3 unknown;
    nothing in M1 constrains it beyond keeping the builder signatures stable.
14. The generator that produces the JS surface from `[NowBuilder]`/`[NowConsumer]`/`[NowScope]` metadata, gated on the
    checked-in public-API delta.
15. Multi-canvas / multi-surface hosting in one page: relax the scene stubs' `internal` constructors or add
    `NowOverlay.Host(object)`; decide what a host object is when a page hosts two NowUI surfaces.
16. A stated rule for a JS caller acting on last frame's results while the core has already advanced
    (`Time.frameCount` is stamped throughout `NowControlState`, `NowInteractionRepaintTracker`,
    `NowSdfImageField.bakeFrame` and the `NowOverlay` registries).

**M4 (cleanup)**

17. The interface refactor specified in §4.9 — `INowNativeInputBridge`, `INowOverlayHost` (with `Component`-typed
    compatibility overloads, without which the Unity test assemblies stop compiling), `INowEventBufferedInputProvider`,
    `INowDeviceInputSource`, and `owningSelection : object` — which deletes the three stand-ins and the scene stubs.

---

## 12. Lead sign-off on the questions this design left open

Recorded 2026-09-07. These are binding for M1 in the same way D1-D8 are, and answer the items the synthesis listed as
outstanding. Where a decision matches what the design already assumed, it is confirmed rather than changed.

**12.1 Existing Unity test sources are read-only (D9).** `Assets/NowUITests` may not be edited in M1. Those tests are the
oracle for exit criterion D8(c): a change to a test invalidates the comparison it is supposed to gate. This is the rule
that makes the rejection of the `INowOverlayHost` retyping correct, and it applies to every unit, not just that one.

Two consequences follow, and both are the intended behaviour rather than a loophole:

- The standalone `Tests` project may add **new** files of its own (`LogAssert`, the performance-testing stand-ins, the
  host and resource support classes). It compiles the existing test sources unmodified.
- A test that cannot run standalone is **left out of the compile list**, never edited into compliance. If a test can only
  pass by changing it, that is a finding about the shim and must be reported, not absorbed.

**12.2 The four allocation-zero cases stay in the gate.** Confirmed as designed. They are the only early warning for M2
browser performance that costs nothing to keep. If U26/U27 triage cannot reach zero, do not add the `[Category]` opt-out
on your own initiative: report the measured allocation, its source, and why the shim cannot avoid it, and stop for a
decision. An opt-out taken silently would remove the signal precisely when it first has something to say.

**12.3 Warnings-as-errors stays off for M1.** Confirmed, with the count recorded in the M1 report. A ratchet during
bring-up would convert every shim gap into a build failure and fight the compile-driven loop, which is the one process
that is doing useful work at that stage.

**12.4 The new `.meta` files are staged, not committed.** Confirmed. Print the `git add` and `git commit` commands and
hand them over. Commits in this repository are the user's call.

**12.5 `NowNumericExpression` parity stays open until measured.** Confirmed: U27 answers it. In Unity some of those 61
golden values come from `UnityEngine.ExpressionEvaluator` and the standalone build uses the managed fallback for all of
them, so the question is whether the fallback agrees on every case. If it does not, port the evaluator per
`GradientCurveSemantics.md` §8. Do not edit the test, and do not narrow the case list (12.1).

**12.6 Float precision policy for the shim (revised after implementation).** The shim reproduces the `[verified]` oracles
**bit-exactly**, using explicit `(double)` widening in the source wherever that is what reproduces them. `SmoothDamp` in
`Vector2`, `Vector3` and `Mathf` is written this way and matches all four captured bit patterns.

This reverses my first instruction, and the reversal is the point. I originally told the implementers to use pure single
precision on the grounds that the shim runs on CoreCLR and WebAssembly while the oracles came from the Mono editor. Two of them
independently found that pure single precision misses those oracles by one to two ulp, and the review pass then converged all
three files onto explicit widening. The argument I had made does not survive that evidence:

- Explicit `(double)` casts in the source are fully specified by the language. They are not the x87 or JIT-dependent behaviour I
  was trying to avoid, so they produce identical results on CoreCLR, WebAssembly and IL2CPP alike. Portability was my main
  reason for preferring single precision, and it does not distinguish the two options.
- The Unity editor is the only environment we have actually measured. Choosing to be one ulp away from the sole oracle, in the
  name of matching runtimes nobody has measured, trades real fidelity for a hypothesis.

So the rule is: match the verified value; where that requires wider intermediates, write the widening explicitly and comment why,
naming this section. Assert bit-exactly. A member that cannot reach its verified value by any documented operation order is a
finding to report, not a tolerance to widen. Where no verified oracle exists, stay in single precision and keep the spec's
operation order.

**12.7 `EventType` touch values follow Unity, not this document.** Section 3.2 numbers the touch block `TouchDown = 80` through
`TouchStationary = 85`. Unity 6000.4 numbers them 30 through 35. The implementer read the constants out of the installed
`UnityEngine.IMGUIModule.dll` metadata, implemented Unity's values, and documented the divergence in the file, the XML comment
and a test. That is correct and is hereby the rule: where this document and the installed Unity assemblies disagree on a numeric
value, **Unity wins**, because section 3.2's own governing sentence says the values are Unity's and are normative. Report the
divergence rather than silently following either source.

Nothing in NowUI reads those members today, so the fix has no behavioural consequence. It is recorded because the same class of
error in a value NowUI *does* read would be silent, and because it establishes which artefact is authoritative.

**12.8 The `NowFilePicker` split row in section 5.2 is incomplete.** Emulating the split found two further methods that touch
`entry.request` and `entry.operation` and that stay in the core half, so the split exactly as written does not compile:

- `TrimThumbnailCache`, which aborts and disposes the oldest entry's request.
- `CancelThumbnailRequests`, which aborts, disposes, and nulls both fields.

Apply the same treatment section 5.2 already prescribes for `NowLottieCache`: extract one `AbortThumbnailRequest(ThumbnailEntry)`
that each half implements, and route both call sites through it. This gap is specific to the `NowFilePicker` row; the
`NowLottieCache` row is complete.

The finding also validates the method that produced it. The units did not merely write code against the plan, they emulated the
plan's file surgery in a scratch copy and compiled it, which is what exposed a gap that reading could not.
