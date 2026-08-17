# Text Preprocessor

`Now.SetTextPreprocessor` registers one hook through which every UI string
passes before it is measured or drawn. Labels, buttons, menus, tooltips,
wrapped text, rich text, and markdown all resolve through it, and resolution
happens before layout, so controls size themselves for the transformed text —
a button measured for "Save" never renders a clipped "Guardar".

Localization is the canonical use, but the hook is a general string
transform: terminology enforcement, casing policies, profanity filtering, or
pseudo-localization for layout testing all fit the same seam.

```csharp
static readonly Dictionary<string, string> Spanish = new()
{
    ["Save"] = "Guardar",
    ["Cancel"] = "Cancelar",
    ["Settings"] = "Ajustes",
};

void Awake()
{
    Now.SetTextPreprocessor(value =>
        Spanish.TryGetValue(value, out var translated) ? translated : value);
}

void OnLanguageChanged()
{
    Now.InvalidateTextPreprocessor();
}
```

Nothing else changes at call sites — `NowLayout.Button("Save").Draw()` now
measures and renders "Guardar".

## Lifecycle

- `Now.SetTextPreprocessor(hook)` registers the hook and invalidates. Passing
  null (or calling `Now.ClearTextPreprocessor()`) unregisters; strings render
  verbatim again.
- `Now.InvalidateTextPreprocessor()` drops every memoized result and requests
  a repaint, so all visible text re-resolves. Call it whenever the hook's
  output changes — typically a language switch. There is no hidden change
  detection; this call is the only trigger.
- `Now.PreprocessText(value)` applies the hook directly. Use it when
  composing text yourself before a raw or span draw.

## Memoization

NowUI memoizes results per unique string, so in steady state the hook runs
once per new string, not once per visible string per frame — the hook body
can afford a dictionary lookup or even an allocation. Memoized output
instances are stable between invalidations, which keeps the shaping caches
keyed by string warm. Returning null or throwing falls back to the source
string (exceptions are logged once per string).

## What bypasses the hook

Some text must never be transformed, and NowUI skips it by design:

- **Editable content** — text fields and text areas draw what the user typed;
  caret and selection indices always match the rendered string. Placeholders
  still resolve, so `"Search..."` localizes.
- **Numeric and span draws** — `Draw(int)`, `Draw(float)`, and
  `ReadOnlySpan<char>` draws (`NowTextBuffer` counters, timers) take the
  allocation-free path with no string identity to key on. Localize dynamic
  text at the format-string level before composing, with
  `Now.PreprocessText` if the pieces come from UI copy.
- **Internal fragments** — word-wrap runs, rich-text segments, and markdown
  ops draw pieces of an already-resolved source; the hook only ever sees
  whole source strings, never fragments like `"Guar"`.

Opt any single draw out with `SetRaw()` — for identifiers, code, or user
names that happen to collide with translation keys:

```csharp
Now.Text(rect).SetRaw().Draw(player.name);
NowLayout.RichText(commitMessage).SetRaw().Draw();
```

## Markdown and rich text

Sources resolve whole, before parsing: the hook sees the complete markdown or
markup document, translates it (keeping the structure), and NowUI parses the
result. Drawn markdown documents cache by resolved text, so an invalidation
re-parses automatically. The retained `NowMarkdown.Parse(...)` snapshot
resolves once at parse time and does not follow later invalidations —
re-parse after a language switch. `NowMarkdownDocument.Parse(...)` is the
verbatim escape hatch.

NowUI's own built-in strings ("Copy", "Select All", "Copied!") pass through
the hook too, so context menus localize with everything else.
