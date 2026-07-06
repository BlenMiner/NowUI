# Code editor

`NowUI.Extensions.CodeEditor` (its own assembly under
`Assets/NowUI/Extensions/`) is an embeddable code editor: syntax
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
"save only when valid" is one if.

Builder options: `SetHeight` / `SetWidth` (stretch width by default in
layout flow), `SetFontSize` (default 14), `SetLineNumbers(false)`,
`SetStatusBar(false)`.

## Docs Scene Demo

Open `Assets/Scenes/DocsScene.unity` and select **Editor demo**. The page is
rendered through NowUI and includes live JSON, Markdown and Markup editors. The
Markup section also renders the edited source through `NowMarkup` below the
editor, so tag completions and validation can be checked against a live UI.

## What the editor does

- **Highlighting** through the language profile, with state carried across
  lines (multi-line constructs color correctly).
- **Validation squiggles** under each diagnostic; hover one for the message,
  or read the status bar — clicking the status error jumps the caret to it.
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
  lines, Shift+Tab dedents.
- **Smart Home** jumps to the first non-space character, then column zero.
- **Line shortcuts**: Ctrl+D duplicates the current line (or the selected
  lines) below; Ctrl+C / Ctrl+X with no selection copy / cut the whole line
  (newline included).
- **Held-key repeat** applies to newlines and Tab as well as characters, so
  holding Enter or Tab keeps inserting (matching how holding a letter
  repeats).
- **Undo/redo** (Ctrl+Z / Ctrl+Y or Ctrl+Shift+Z) with typing coalesced
  into single steps.
- Line numbers, current-line highlight, two-axis scrolling with the caret
  kept in view, click/drag selection (double-click a word, triple-click or
  click the gutter for a line), clipboard, IME composition, focus
  integration — the same conventions as TextField and TextArea.

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

    public override void Validate(string text, List<NowCodeDiagnostic> diagnostics) { /* optional */ }
}

NowCodeLanguage.Register(new MyIniLanguage());   // findable by markdown fences too
```

Override `aliases` to add alternate registry keys, `autoPairs` to change the
auto-close set, `TryComplete` for IDE-style character completions, and
`IsIndentOpener`/`IsIndentCloser` to teach Enter your block characters.

The editor renders with the theme font at per-codepoint metrics; assign a
monospace face via the theme for the classic look — everything works either
way.
