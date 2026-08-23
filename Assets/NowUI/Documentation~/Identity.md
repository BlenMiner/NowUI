# Identity

NowUI identity has two deliberately separate phases:

- `NowId` is an authored, local key: a non-empty string, any integer (including zero),
  or `default` to use the caller site.
- `NowResolvedId` is the opaque runtime identity after NowUI has included the
  active owner, identity scopes, subsystem domain, and full path ancestry.

The type boundary is the safety rule: author with `NowId`, resolve once, and
then derive children from the resulting `NowResolvedId`. A resolved value has
no public integer conversion or raw-value constructor, so it cannot be
accidentally fed back through host scoping.

| Type | Meaning | Valid use |
| --- | --- | --- |
| `NowId` | Authored local path segment, or `default` for call-site fallback | Stable string/model-integer keys; integer zero is valid |
| `NowCallSiteId` | Opaque captured source location | Custom-builder fallback resolution only; process-local and not persisted |
| `NowResolvedId` | Owner-, domain-, and ancestry-qualified runtime path | Focus, interaction, state, layout, effects, menus, and derived `.Child(...)` paths |
| `NowControlIdentity` | Builder carrier containing either `NowId` or `NowResolvedId` | Preserve authored-vs-resolved intent until `Draw()` resolves once |
| `NowTreeNodeKey` | Semantic caller-owned tree data path | Selection/expansion state; independent of UI hosts and control identity |

An integer is not inherently a legacy ID. An integer passed where `NowId` is
expected is an authored local key, and zero is valid. An integer passed to an
old API that treated it as a fully resolved control, focus, state, menu, or
overlay identity is rejected at compile time. Migrate according to meaning,
not storage type.

```csharp
NowId authored = item.id;
NowResolvedId row = NowControls.GetControlId(authored);
NowResolvedId remove = row.Child("remove");

var rowInteraction = NowInput.Interact(row, rowRect);
var removeInteraction = NowInput.Interact(remove, removeRect);
```

Resolved IDs are runtime-only. Do not serialize them or use them as model
identity. Keep the original application key and reconstruct the path on each
run.

## Ownership and isolation

Every input provider and retained host owns a distinct root. The same local
`NowId` therefore cannot share focus, pointer capture, state, layout caches, or
overlays across panels merely because its text or integer matches. Nested
`IdScope` and `ControlScope` values add path segments under that owner.

Subsystems also use separate domains. A control path, layout cache, state slot,
effect, focus host, and overlay cannot alias by producing the same authored
integer. Path derivation is ordered and non-cancelling; `a.Child(b)` is not the
same identity as `b.Child(a)`.

Legacy APIs accepting raw resolved-looking `int` values are source-blocked
with compiler errors. Migrate the value according to what it means: wrap an
authored integer in `NowId` and resolve it under the current host, or keep an
already-resolved value as `NowResolvedId`. Do not cast, hash, or import an old
integer as though it were a resolved identity.

## Call-site fallback has its own type

Id-less controls resolve from their caller file and line. Loops over one call
site receive occurrence salting, so this fallback follows draw position. It is
appropriate for fixed one-off controls, not data that can reorder.

`NowControls.SiteId(file, line)` interns that source location into an opaque,
process-local `NowCallSiteId` for custom builders. The value is neither a
`NowId` nor a `NowResolvedId`: store it only as the fallback beside a
`NowControlIdentity`, then pass it to typed fallback APIs such as
`_id.Resolve(_site)`. It exposes no integer conversion or token accessor. Do
not persist it or derive children from it.

## Repeated and reorderable data

Call-site identity is ideal for one-off controls. A repeated call site is
occurrence-salted by draw order, which is safe only while logical items retain
their positions. Key reorderable UI by model identity:

```csharp
foreach (var item in inventory)
{
    using (NowControls.KeyedItem(item.id))
    {
        NowLayout.Label(item.name).Draw();

        if (NowLayout.Button("Remove").Draw())
            Remove(item.id);
    }
}
```

`KeyedItem(key)` uses its caller site as the list namespace. When several
helpers or call sites render the same logical list, name that namespace
explicitly:

```csharp
using (NowControls.KeyedItemIn("inventory", item.id))
    DrawInventoryRow(item);
```

Both APIs reject a missing key. Use `IdScope` for a general reusable panel;
prefer `KeyedItem`/`KeyedItemIn` when the scope represents collection data.

## Custom builders

Store `NowControlIdentity` when a builder accepts either authored or resolved
identity. It is a carrier for those two cases, not a third identity phase. Its
default value means "use the captured call-site fallback"; it preserves the
distinction until the consumer resolves the control:

```csharp
[NowBuilder]
public struct MyControl
{
    readonly NowCallSiteId _site;
    NowControlIdentity _id;

    internal MyControl(NowCallSiteId site)
    {
        _site = site;
        _id = default;
    }

    public MyControl SetId(NowId id) { _id = id; return this; }
    public MyControl SetId(NowResolvedId id) { _id = id; return this; }

    public bool Draw(NowRect rect)
    {
        NowResolvedId id = _id.Resolve(_site);
        var interaction = NowControls.Interact(
            id, rect, out bool focused, out bool submitted);
        // Draw from interaction, focused, and submitted.
        return interaction.clicked || submitted;
    }
}
```

Use `resolved.Child(...)` for private sub-controls. Do not hash strings into
integers, combine integer IDs manually, or reuse an overlay callback payload as
overlay identity.

## Composite interaction

Immediate-mode declaration order is not a z-order system. When a parent row
contains independent child controls, exclude their hit rectangles from the
parent before interacting with it:

```csharp
var rowRegion = NowInteractionRegion.From(rowRect)
    .Exclude(toggleRect)
    .Exclude(menuButtonRect);

var row = NowControls.Interact(
    rowId, in rowRegion, default,
    out bool focused, out bool submitted);
```

This prevents the parent from hovering, pressing, or clicking through a child,
including the press frame and release frame. The value type stores up to four
exclusions inline without a managed collection.

## Context menus and overlays

Resolve a menu source once, keep callback payloads separate, and give every
interactive entry a stable authored ID:

```csharp
NowResolvedId menuId = NowControls.GetControlId("selection-menu");
NowContextTrigger trigger = NowContextAction.Resolve(
    in rowRegion, actionInvoked, actionRect);

NowContextMenu.Open(menuId, trigger);

if (NowContextMenu.Begin(menuId))
{
    if (NowContextMenu.Item("Rename", id: "rename"))
        Rename();

    if (NowContextMenu.BeginSubmenu("Create", id: "create"))
    {
        if (NowContextMenu.Item("Folder", id: "folder"))
            CreateFolder();
        NowContextMenu.EndSubmenu();
    }

    NowContextMenu.End();
}
```

`NowContextAction.Resolve` keeps secondary-pointer and explicit-action origins
distinct: pointer menus open at the pointer, while action menus anchor beside
their control. Its composite-region overload also prevents a parent's
secondary-click action from stealing input inside excluded child controls.
Stable item IDs keep deferred click delivery correct when a label is localized
or siblings are inserted, removed, or reordered.

APIs that recognize a secondary action should return the trigger itself, not a
detached `bool` and pointer position. For example,
`NowTextSelectionResult.contextTrigger` can be passed directly to
`NowContextMenu.Open(menuId, result.contextTrigger)`. Its former
`rightClicked` and `rightClickPosition` properties are source-blocked because
they discard the ownership needed to claim the handled press.

The positional `Item(...)` and `BeginSubmenu(...)` forms are source-blocked.
Use a named `id:` argument (or pass a `NowId` explicitly); this also prevents a
two-string item call from accidentally treating the intended ID as a shortcut.
Menu IDs themselves are resolved source IDs, never raw integers.

Opening from a secondary-pointer trigger claims only that secondary press for
the current provider and input pass. Successful interactions likewise claim
their captured button, so controls declared later cannot react to the same
press while other buttons and providers remain independent. This ordering rule
does not replace `NowInteractionRegion`: a parent declared first can still
react, and parent hover/release semantics also need explicit child exclusions.

An open menu belongs to the overlay registration owner and input provider that
opened it. A retained host may stay idle without losing the menu. Once that
same owner completes another successful declaration pass, it must call
`Begin(menuId)` and declare the menu again or the menu closes. A failed pass
keeps the last completed menu and blocking footprint. A clicked item is
delivered only during that owner's next declaration pass on the same provider;
unrelated owners cannot receive or discard it, and an omitted item expires.

Deferred overlays retain their declaration context. When a queued callback is
flushed, NowUI restores the originating input provider, input snapshot/pass,
surface mapping, host, theme, transform, and resolved ID scope around the
callback, then restores the outer context afterward. Input reads in a nested or
late callback therefore belong to the surface that queued it.

Keep overlay identity separate from callback data:

```csharp
// Anonymous overlay: rowIndex is callback payload, not identity.
NowOverlay.Defer(popupRect, rowIndex, DrawPopup);

// Named overlay: sourceId owns the overlay; rowIndex is still payload.
NowOverlay.Defer(popupRect, sourceId, rowIndex, DrawPopup);
```

The same rule applies to `DeferScreen` and `DeferPassive`. If the overload has
only an `int state`, the callback is anonymous. Do not reuse that integer as an
overlay key or assume it participates in pointer-block ownership.

## Semantic state is not UI identity

Caller-owned state should store model keys, not host-resolved IDs. Tree views
use `NowTreeNodeKey` for this reason:

```csharp
NowTreeNodeKey assets = NowTreeNodeKey.From("assets");
NowTreeNodeKey readme = assets.Child("readme");

treeState.SetExpanded(assets, true);
bool selected = treeState.selectedKey == readme;
```

These keys describe the data path independently of the host. Reconstruct them
from persistent application IDs rather than serializing the opaque key itself.

## Migrating from integer composition

| Old pattern | Replacement |
| --- | --- |
| `NowId.Resolved(value)` | Pass the `NowResolvedId` value directly |
| `int id = NowControls.GetControlId(...)` | `NowResolvedId id = ...` |
| `int site = NowControls.SiteId(file, line)` | `NowCallSiteId site = NowControls.SiteId(file, line)` |
| `NowInput.CombineId(parent, child)` | `parent.Child(child)` |
| `NowInput.GetId(parent, "child")` | `parent.Child("child")` |
| `NowInput.activeId == 0` | `!NowInput.activeId.hasValue` |
| `NowFocus.focusedId` | `NowFocus.focusedResolvedId` |
| `NowControlState.Get<T>(intId)` | Resolve the authored key, then call `Get<T>(NowResolvedId)` |
| `NowFocus.Focus(intId)` / `Register(intId, ...)` | Resolve the authored key, then use the `NowResolvedId` overload |
| `NowTooltip.For(intId, ...)` | `NowTooltip.For(resolvedId, ...)` |
| `NowOverlay.Defer(rect, intValue, callback)` used as a named overlay | `Defer(rect, resolvedSourceId, callback)`; the `int` overload is anonymous payload |
| One integer used for named overlay ID and callback state | `Defer(rect, resolvedSourceId, state, callback)` |
| Positional context-menu items/submenus | `Item(label, id: stableKey)` / `BeginSubmenu(label, id: stableKey)` |
| `selection.rightClicked` plus `rightClickPosition` | Pass `selection.contextTrigger` directly to `NowContextMenu.Open` |
| Loop identity from draw order | `KeyedItem(key)` or `KeyedItemIn(list, key)` |
| Tree selection stored as a control integer | `NowTreeNodeKey` / `selectedKey` |
