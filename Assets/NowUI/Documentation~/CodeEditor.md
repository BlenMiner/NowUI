# Code editor

`NowUI.Extensions.CodeEditor` (its own assembly under
[`Extensions/CodeEditor`](../Extensions/CodeEditor)) is an embeddable code editor: syntax
highlighting, validation squiggles, auto-closing pairs and the rest of the
IDE sugar that makes editing a config file painless — drawn entirely through
`Now`/`NowLayout` primitives.

## Usage

```csharp
using NowUI.CodeEditor;

// In layout flow — stretches to the available width:
var result = NowCode.Editor(NowJsonLanguage.instance).SetHeight(280).Draw(ref jsonText);

if (result.changed && result.isValid)
    Save(jsonText);

// Explicit rect, markdown profile:
NowCode.Editor(rect, NowMarkdownCodeLanguage.instance).Draw(ref readme);

// Markup / XML-like NowUI source:
NowCode.Editor(rect, NowMarkupCodeLanguage.instance).Draw(ref markupText);
```

`NowCodeEditorResult` reports `changed`, `isValid` and `diagnosticCount`, so
"save only when valid" is one if. `isValid` means *no errors*: diagnostics
carry a `NowCodeDiagnosticSeverity` (`Error`, `Warning`, `Info`), and
warnings or infos advise without failing the gate.

Builder options: `SetHeight` / `SetWidth` (stretch width by default in
layout flow), `SetFontSize` (default 14), `SetLineNumbers(false)`,
`SetStatusBar(false)`, and authored or resolved `SetId(...)` overloads.

Each explicitly identified editor retains its parsed line table and undo
history between draws. Retention is bounded to the 128 most recently drawn
editors by default; tune `NowCodeEditor.cacheCapacity` for unusually large
editor grids. When a dynamic editor is removed permanently, call
`NowCodeEditor.ReleaseCache(id)` from the same host/id scope (or pass a fully
resolved `NowResolvedId`) to release it immediately. `ResetCaches()` releases every
editor cache.

## Example

The packaged [docs browser source](../Example/NowDocsExample.cs) includes live
JSON, Markdown, and Markup editors. Its Markup section also renders the edited
source through `NowMarkup`, so completions and validation can be compared with
the resulting UI.

## What the editor does

- **Highlighting** through the language profile, with state carried across
  lines (multi-line constructs color correctly).
- **Validation squiggles** under each diagnostic, colored by severity —
  errors red, warnings amber through the theme's `Warning` token, infos
  muted. Hover one for the message (the worst wins an overlap), or read the
  status bar, which shows the worst problem in its severity's color —
  clicking it jumps the caret there.
- **Auto-close pairs**: typing `{`, `[`, `(` or `"` inserts the pair with
  the caret between; typing the closer over an auto-closed one skips it;
  Backspace inside an empty pair deletes both; typing an opener with a
  selection wraps the selection. Language profiles can add richer completions:
  the markup profile completes `<tag>` to `<tag></tag>`, supports `<tag />`,
  keeps void tags self-contained, and completes `</` to the nearest open tag.
- **Enter auto-indents**, keeping the current line's indentation;
  Enter between matching block delimiters expands with an indented middle line,
  including between markup opening and closing tags.
- **Tab** inserts four spaces; with a multi-line selection it indents the
  lines, Shift+Tab dedents. The focused editor owns Tab, so it never traverses
  focus while performing these actions.
- **Smart Home** jumps to the first non-space character, then column zero.
- **Line shortcuts**: Ctrl+D duplicates the current line (or the selected
  lines) below; Ctrl+C / Ctrl+X with no selection copy / cut the whole line
  (newline included).
- **Held-key repeat** applies to newlines and Tab as well as characters, so
  holding Enter or Tab keeps inserting (matching how holding a letter
  repeats).
- **Quick actions** contributed by the language: the right-click menu lists
  them under Rename Symbol, and Alt+Enter opens the same list in a popup at
  the caret (Up/Down selects, Enter applies, Escape closes). An action applies
  all of its edits as one undo step. A plain Enter always breaks the line —
  only the chord and an open popup reach the action.
- **Undo/redo** (Ctrl+Z / Ctrl+Y or Ctrl+Shift+Z) with typing coalesced
  into single steps.
- Line numbers, current-line highlight, two-axis scrolling with the caret
  kept in view, click/drag selection (double-click a word, triple-click or
  click the gutter for a line), clipboard, IME composition, focus
  integration — the same conventions as TextField and TextArea. Escape first
  closes the active go-to-line or completion layer, then leaves the editor;
  while an IME composition is active, Escape belongs to the composition
  instead.

## Languages

`NowJsonLanguage` tokenizes property names, strings, numbers and literals
distinctly and validates with a full parser: missing commas, trailing
commas, unterminated strings, bad escapes, leading zeros, comments and
trailing content all produce positioned, human messages.

`NowMarkdownCodeLanguage` highlights markdown *source* — headings, emphasis,
inline code, links, quotes, list markers — and delegates fenced code blocks
to the registered language of their info string, so a `json` fence
highlights as JSON inside the markdown editor. It warns on unclosed fences.
(To *render* markdown, use the markdown extension; the docs browser demo
pairs both behind a preview toggle.)

`NowMarkupCodeLanguage` highlights NowUI markup/XML-like source: tag names,
attributes, strings, entities, comments and CSS inside `<style>` blocks. It
validates balanced non-void tags and registers aliases `nowui`, `xml`, `html`
and `uxml`, so markdown fences such as `nowui` delegate to the markup
highlighter.

## Adding a language

Derive from `NowCodeLanguage` and register it:

```csharp
public sealed class MyIniLanguage : NowCodeLanguage
{
    public override string name => "ini";

    public override int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens)
    {
        // Emit NowCodeToken ranges (sparse is fine — gaps render plain).
        // Return the state to carry into the next line (0 if stateless).
        return 0;
    }

    public override void Validate(string text, List<NowCodeDiagnostic> diagnostics)
    {
        // Optional. Severity defaults to Error; a warning renders amber and
        // leaves result.isValid true.
        // diagnostics.Add(new NowCodeDiagnostic
        // {
        //     start = 0, length = 3, message = "Prefer lowercase keys",
        //     severity = NowCodeDiagnosticSeverity.Warning
        // });
    }
}

NowCodeLanguage.Register(new MyIniLanguage());   // findable by markdown fences too
```

Override `aliases` to add alternate registry keys, `autoPairs` to change the
auto-close set, `TryComplete` for IDE-style character completions, and
`IsIndentOpener`/`IsIndentCloser` to teach Enter your block characters.

Override `TryGetCodeActions` to contribute quick actions — the rows the
context menu lists under Rename Symbol and Alt+Enter opens at the caret:

```csharp
public override bool TryGetCodeActions(string text, int caret, List<NowCodeAction> actions)
{
    actions.Add(new NowCodeAction
    {
        id = "implement-ibar",                 // stable, and never a title
        title = "Change 'struct' to 'class' and implement IBar",
        detail = "IBar",
        // Ranges index into this text and must not overlap; the editor
        // applies them from the highest offset down, so both land. List the
        // edit the author should end up in first — caretOffset is measured
        // into its text.
        edits = new[]
        {
            new NowCodeEdit(bodyStart, 0, "\n    public void Tick() { }\n"),
            new NowCodeEdit(headerStart, "struct".Length, "class")
        },
        caretOffset = 5
    });

    return true;
}
```

Ids must be unique within the list: rows deliver their click by id one pass
after the menu closes, so two actions may share a title but never an id.

The editor renders with the theme font at per-codepoint metrics; assign a
monospace face via the theme for the classic look — everything works either
way.
