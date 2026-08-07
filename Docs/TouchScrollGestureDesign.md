# Touch Scroll Gesture Arbitration

> Internal design note for unshipped work. The current public behavior remains
> unchanged until this design is implemented and validated.

## Why this is not a `ScrollView`-only fix

The device reader currently distinguishes the mouse from a touch by
`NowMouseInput.pointerSource`, but that provenance is not carried into
`NowInputSnapshot`. By the time a control calls `NowInput.Interact`, every
primary pointer looks the same.

An enclosing scroll view also processes panning when its scope is disposed,
after its children have interacted. At that point a child may already have
focused, opened `TouchScreenKeyboard`, changed a held-driven value such as a
slider, or started a custom drag. Clearing its active ID there would prevent a
later click, but it could not undo those side effects. Applying that behavior
to every primary drag would also regress mouse controls.

The safe boundary is therefore a small gesture arbiter between input capture
and child activation.

## Required invariants

- Mouse and pen interaction keeps its current immediate press/drag behavior.
- A touch tap on a child activates that child exactly once.
- A touch scroll that starts on a child never clicks, focuses, opens a keyboard,
  or mutates that child before the scroll wins.
- A scroll win cancels the child interaction; it never fabricates a release or
  click. Native capture loss remains a cancellation too.
- Host capture remains owned by the UGUI, UI Toolkit, world, screen, or IMGUI
  host. The arbiter only chooses the NowUI recipient of the already-routed
  stream.
- Nested scroll views give the innermost region first refusal without breaking
  ordinary edge handoff to an ancestor.
- The warmed interaction path remains allocation-free.

## 1. Preserve pointer provenance

Add a pointer-kind value to `NowInputSnapshot` (unknown, mouse, touch, pen).
Existing constructors and custom providers should default to `Unknown`, which
must preserve today's immediate behavior. Built-in providers populate it as
follows:

- screen, RectTransform, and world providers translate the device reader's
  mouse sentinel or touch ID;
- the UI Toolkit provider buffers `PointerEventBase.pointerType` with the rest
  of the event;
- the IMGUI provider reports mouse;
- custom providers can explicitly emit touch for remote/mobile surfaces.

Only `Touch` participates in automatic scroll arbitration. Pen input should
remain direct by default.

## 2. Register contenders before activation

`NowScrollView.Begin()` registers a touch-scroll contender containing the
provider identity, resolved scroll ID, transformed content viewport, available
axes/ranges, and nesting depth. Registration happens before children draw, but
only the innermost eligible region under the press becomes the initial scroll
contender.

The first child `Interact` hit records the child contender without immediately
publishing `pressed` or `held`. The interaction API needs an internal gesture
policy so built-in controls can declare intent:

- tap controls allow scroll handoff;
- direct controls declare the axes they manipulate (for example, a horizontal
  slider inside a vertical list);
- existing public/custom interaction defaults remain immediate for source
  compatibility, with an explicit opt-in overload for custom touch-aware
  controls.

While unresolved, controls may render a pending-touch visual, but they must not
commit focus or value changes. Standard focus moves on a confirmed tap or when
the child wins direct manipulation. Text controls therefore open the software
keyboard only after a confirmed tap, not on the tentative down.

## 3. Resolve by threshold and direction

Use `NowInput.dragThreshold` in surface coordinates and retain the original
press position. Below the threshold the gesture remains a possible tap.

- Movement along an available scroll axis awards the gesture to the scroll
  contender. Axis dominance prevents a vertical list from stealing a clearly
  horizontal slider drag.
- Movement along a child's declared direct axis awards it to the child and
  delivers the accumulated displacement as the start of its drag.
- Release below the threshold resolves as one child tap.
- Cancel, provider change, host focus loss, or native capture loss resolves as
  cancellation with no activation.

When scrolling wins, the child receives one cancelled transition if it had a
visible pending state, the active NowUI recipient becomes the scroll gesture,
and the full displacement since the press is applied so the threshold does not
create a dead jump. Do not release or reacquire the host's native capture.

For nested scroll views, choose the innermost contender that can move in the
gesture direction. If it is clamped from the outset, allow the ancestor to win;
after a region has won, keep ownership stable until release rather than
oscillating between regions. Modal popup scroll ownership remains containing.

## 4. Flick continuation

Track a short, fixed-capacity history of touch deltas and timestamps while the
scroll owns the gesture. On release, derive a capped velocity and decay it with
a time-based curve. Stop continuation on a new press, cancel, provider change,
directional clamp, or a negligible velocity. Request repaints only while the
offset can still change, including one final repaint on first reaching a clamp.

The history and contender stacks should use pre-sized value storage, not
per-gesture collections.

## Focused validation

Add input-core tests before host-specific tests:

- mouse drag on a slider/text selection is unchanged;
- touch tap on a button clicks once, and a touch tap on a text field focuses
  and opens the keyboard only after resolution;
- vertical touch drag beginning on a button scrolls and never clicks/focuses it;
- vertical touch drag beginning on a text field never opens the keyboard;
- horizontal slider drag wins inside a vertical scroll view, while a vertical
  gesture over the same slider scrolls without changing its value;
- cancellation and native capture loss produce no click or flick continuation;
- an inner region wins when movable, and a clamped inner region yields to its
  ancestor;
- inertia stops at both clamps and does not request idle repaints.

Then exercise the same tap-versus-scroll fixture through screen, UGUI
RectTransform, UI Toolkit, and world providers. IMGUI should verify that its
mouse behavior and native capture lifecycle are unchanged. Include a custom
provider test proving `Unknown` preserves legacy semantics and explicit
`Touch` opts into arbitration.
