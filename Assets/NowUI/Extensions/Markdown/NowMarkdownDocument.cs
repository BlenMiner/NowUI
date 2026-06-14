using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI.Markdown
{
    public struct NowMarkdownResult
    {
        /// <summary>Laid-out content height for the width it was drawn at.</summary>
        public float height;

        /// <summary>Destination of a link clicked this frame, or null.</summary>
        public string clickedLink;

        /// <summary>Destination of the link under the pointer, or null.</summary>
        public string hoveredLink;
    }

    /// <summary>
    /// Visual sizing for a rendered document; colors come from the ambient
    /// <see cref="NowTheme.themeAsset"/> at draw time.
    /// </summary>
    public struct NowMarkdownStyle
    {
        public float fontSize;

        public static NowMarkdownStyle Default => new NowMarkdownStyle { fontSize = 15f };
    }

    /// <summary>
    /// A parsed markdown document: parse once, draw every frame. Layout (word
    /// wrap, tables, positions) is cached per width and only recomputed when the
    /// width or font changes, so steady-state drawing allocates nothing.
    /// </summary>
    public sealed class NowMarkdownDocument
    {
        enum OpKind : byte
        {
            Text,
            Fill,
            Line,
            Image,
            CopyButton,
            SelectionLayer
        }

        struct SelectionRegion
        {
            public string literal;
            public int lineStart;
            public int lineCount;
            public float fontSize;
            public NowRect buttonRect;
        }

        enum Role : byte
        {
            Body,
            Heading,
            Code,
            CodePanel,
            Link,
            Muted,
            Rule,
            QuoteBar,
            CheckBox,
            CheckFill,
            Bullet,
            TableLine,
            TableHeaderFill,
            SyntaxKeyword,
            SyntaxString,
            SyntaxNumber,
            SyntaxComment
        }

        struct Op
        {
            public OpKind kind;
            public Role role;
            public NowRect rect;
            public NowRect hoverRect;
            public string text;
            public float fontSize;
            public NowFontStyle fontStyle;
            public int link;
        }

        readonly NowMarkdownBlock _root;
        readonly NowMarkdownStyle _style;
        readonly List<Op> _ops = new List<Op>(64);
        readonly List<string> _links = new List<string>(4);
        readonly List<int> _linkHoverOp = new List<int>(4);
        readonly List<SelectionRegion> _selectionRegions = new List<SelectionRegion>(4);
        readonly List<NowTextSelectionLine> _selectionLines = new List<NowTextSelectionLine>(16);
        readonly List<NowTextSelectionLine> _selectionScratch = new List<NowTextSelectionLine>(16);
        readonly System.Text.StringBuilder _regionBuilder = new System.Text.StringBuilder(128);
        readonly List<NowTextSelectionLine> _documentScratch = new List<NowTextSelectionLine>(32);
        readonly List<NowRect> _exclusionScratch = new List<NowRect>(2);
        readonly NowRichTextLayout _richText = new NowRichTextLayout();
        string _documentText = string.Empty;
        bool _regionActive;
        int _regionSegmentStart;
        int _strikeSequence;

        float _layoutWidth = -1f;
        float _layoutHeight;
        NowFontAsset _layoutFont;
        int _imagesVersion = -1;
        bool _hasLoadingImages;

        public NowMarkdownDocument(NowMarkdownBlock root, NowMarkdownStyle style)
        {
            _root = root;
            _style = style;
        }

        public static NowMarkdownDocument Parse(string markdown)
        {
            return new NowMarkdownDocument(NowMarkdownParser.Parse(markdown), NowMarkdownStyle.Default);
        }

        public static NowMarkdownDocument Parse(string markdown, NowMarkdownStyle style)
        {
            return new NowMarkdownDocument(NowMarkdownParser.Parse(markdown), style);
        }

        /// <summary>The height of the last layout; 0 before the first draw.</summary>
        public float lastHeight => _layoutHeight;

        /// <summary>Lays out (cached) and returns the content height for a width.</summary>
        public float MeasureHeight(float width)
        {
            EnsureLayout(width);
            return _layoutHeight;
        }

        /// <summary>
        /// Draws the document into the rect (content wraps to rect.width; height
        /// is reported in the result, content below rect.height simply overflows
        /// unless an ambient mask clips it).
        /// </summary>
        public NowMarkdownResult Draw(NowRect rect)
        {
            var result = default(NowMarkdownResult);
            var theme = NowTheme.themeAsset;

            EnsureLayout(rect.width);
            result.height = _layoutHeight;

            if (_layoutFont == null)
                return result;

            if (_hasLoadingImages)
                NowControlState.RequestRepaint();

            int docId = NowInput.GetId(GetHashCode(), "markdown");

            // A link spanning several words is ONE link: the prepass finds the
            // word the pointer is over per link, then the link interacts ONCE
            // through that rect (press and release can land on different words)
            // BEFORE any selection layer runs, so pressing a link clicks it
            // instead of starting a text selection.
            _linkHoverOp.Clear();

            for (int i = 0; i < _links.Count; ++i)
                _linkHoverOp.Add(-1);

            for (int i = 0; i < _ops.Count; ++i)
            {
                var op = _ops[i];

                if (op.link < 0 || (op.kind != OpKind.Text && op.kind != OpKind.Image))
                    continue;

                var probe = new NowRect(rect.x + op.rect.x, rect.y + op.rect.y, op.rect.width, op.rect.height);

                if (NowInput.IsHovered(probe))
                    _linkHoverOp[op.link] = i;
            }

            for (int i = 0; i < _ops.Count; ++i)
            {
                var op = _ops[i];

                if (op.link < 0 || (op.kind != OpKind.Text && op.kind != OpKind.Image) ||
                    _linkHoverOp[op.link] != i)
                {
                    continue;
                }

                var target = new NowRect(rect.x + op.rect.x, rect.y + op.rect.y, op.rect.width, op.rect.height);
                var interaction = NowInput.Interact(NowInput.CombineId(docId, op.link), target);
                result.hoveredLink = _links[op.link];
                NowControlState.RequestRepaint();

                if (interaction.clicked)
                    result.clickedLink = _links[op.link];
            }

            InteractDocumentSelection(docId, rect);

            for (int i = 0; i < _ops.Count; ++i)
            {
                var op = _ops[i];
                var target = new NowRect(rect.x + op.rect.x, rect.y + op.rect.y, op.rect.width, op.rect.height);
                bool hovered = op.link >= 0 && op.link < _linkHoverOp.Count && _linkHoverOp[op.link] >= 0;

                switch (op.kind)
                {
                    case OpKind.Text:
                    {
                        var text = theme.Text(target, _layoutFont);
                        text.fontSize = op.fontSize;
                        text.fontStyle = op.fontStyle;
                        text.color = ResolveColor(theme, op.role, hovered);
                        text.SetMask(target.Outset(4f)).Draw(op.text);
                        break;
                    }
                    case OpKind.Fill:
                    case OpKind.Line:
                    {
                        Now.Rectangle(target)
                            .SetColor(ResolveColor(theme, op.role, hovered))
                            .SetRadius(op.role == Role.CodePanel || op.role == Role.CheckBox ? 4f :
                                op.role == Role.Bullet ? op.rect.width * 0.5f : 0f)
                            .Draw();
                        break;
                    }
                    case OpKind.Image:
                    {
                        DrawImage(theme, docId, i, op, target, hovered);
                        break;
                    }
                    case OpKind.CopyButton:
                    {
                        DrawCopyButton(theme, docId, i, op, target);
                        break;
                    }
                    case OpKind.SelectionLayer:
                    {
                        if (op.link >= 0)
                            DrawSelectionRegion(theme, docId, op.link, rect);

                        break;
                    }
                }
            }

            return result;
        }

        void InteractDocumentSelection(int docId, NowRect origin)
        {
            if (_selectionRegions.Count == 0 || _documentText.Length == 0 || _layoutFont == null)
                return;

            _documentScratch.Clear();
            _exclusionScratch.Clear();

            for (int i = 0; i < _selectionLines.Count; ++i)
            {
                var line = _selectionLines[i];
                line.rect = new NowRect(
                    origin.x + line.rect.x,
                    origin.y + line.rect.y,
                    line.rect.width,
                    line.rect.height);
                _documentScratch.Add(line);
            }

            for (int i = 0; i < _selectionRegions.Count; ++i)
            {
                var button = _selectionRegions[i].buttonRect;

                if (button.isEmpty)
                    continue;

                _exclusionScratch.Add(new NowRect(
                    origin.x + button.x,
                    origin.y + button.y,
                    button.width,
                    button.height));
            }

            for (int i = 0; i < _ops.Count; ++i)
            {
                var op = _ops[i];

                if (op.kind != OpKind.Image)
                    continue;

                _exclusionScratch.Add(new NowRect(
                    origin.x + op.rect.x,
                    origin.y + op.rect.y,
                    op.rect.width,
                    op.rect.height));
            }

            int selectionId = NowInput.GetId(docId, "selection");
            var selection = NowTextSelection.Interact(
                selectionId, _documentText, _documentScratch, _layoutFont,
                _style.fontSize, NowFontStyle.Regular, _exclusionScratch);

            int menuId = NowInput.GetId(selectionId, "menu");

            if (selection.rightClicked)
                NowContextMenu.Open(menuId, selection.rightClickPosition);

            if (NowContextMenu.Begin(menuId))
            {
                if (selection.hasSelection && NowContextMenu.Item("Copy"))
                    NowClipboard.Copy(NowTextSelection.GetSelection(selectionId, _documentText));

                if (NowContextMenu.Item("Select All"))
                    NowTextSelection.SelectAll(selectionId, _documentText);

                NowContextMenu.End();
            }
        }

        void DrawSelectionRegion(NowThemeAsset themeAsset, int docId, int regionIndex, NowRect origin)
        {
            var region = _selectionRegions[regionIndex];
            _selectionScratch.Clear();

            for (int l = 0; l < region.lineCount; ++l)
            {
                var line = _selectionLines[region.lineStart + l];
                line.rect = new NowRect(
                    origin.x + line.rect.x,
                    origin.y + line.rect.y,
                    line.rect.width,
                    line.rect.height);
                _selectionScratch.Add(line);
            }

            Color highlight = themeAsset.GetColor(NowColorToken.Accent, Color.blue);
            highlight.a = 0.25f;

            NowTextSelection.DrawHighlights(
                NowInput.GetId(docId, "selection"), _documentText, _selectionScratch,
                _layoutFont, region.fontSize, NowFontStyle.Regular, highlight);
        }

        void DrawCopyButton(NowThemeAsset themeAsset, int docId, int opIndex, in Op op, NowRect target)
        {
            int buttonId = NowInput.CombineId(docId, ~opIndex);
            ref float copiedAt = ref NowControlState.Get<float>(buttonId);
            bool showCopied = copiedAt > 0f && Time.realtimeSinceStartup - copiedAt < 1.2f;

            var panelTarget = new NowRect(
                target.x + op.hoverRect.x - op.rect.x,
                target.y + op.hoverRect.y - op.rect.y,
                op.hoverRect.width,
                op.hoverRect.height);

            if (!NowInput.IsHovered(panelTarget) && !showCopied)
                return;

            if (DrawBadgeButton(themeAsset, buttonId, target, ref copiedAt))
                NowClipboard.Copy(op.text);
        }

        void DrawImage(NowThemeAsset themeAsset, int docId, int opIndex, in Op op, NowRect target, bool linkHovered)
        {
            if (NowMarkdownImages.GetState(op.text, out var texture) != NowMarkdownImageState.Loaded ||
                texture == null)
            {
                return;
            }

            var image = Now.Rectangle(target).SetTexture(texture).SetRadius(4f);

            if (linkHovered)
                image.color = new Vector4(1.08f, 1.08f, 1.08f, 1f);

            image.Draw();

            var snapshot = NowInput.current;
            int menuId = NowInput.CombineId(NowInput.GetId(docId, "img-menu"), opIndex);

            if (!NowInput.isPassive && snapshot.hasPointer && target.Contains(snapshot.pointerPosition) &&
                (snapshot.pointerButtonsPressed & NowPointerButtons.Secondary) != 0)
            {
                NowContextMenu.Open(menuId, snapshot.pointerPosition);
            }

            if (NowContextMenu.Begin(menuId))
            {
                if (NowContextMenu.Item("Copy image address"))
                    NowClipboard.Copy(op.text);

                NowContextMenu.End();
            }
        }

        bool DrawBadgeButton(NowThemeAsset themeAsset, int buttonId, NowRect target, ref float copiedAt)
        {
            var interaction = NowInput.Interact(buttonId, target);
            bool clicked = interaction.clicked;

            if (clicked)
                copiedAt = Time.realtimeSinceStartup;

            bool showCopied = copiedAt > 0f && Time.realtimeSinceStartup - copiedAt < 1.2f;

            if (interaction.hovered || showCopied)
                NowControlState.RequestRepaint();

            var background = themeAsset.Rectangle(target, NowRectangleStyle.Surface);
            background.radius = new Vector4(4f, 4f, 4f, 4f);
            background.outline = 1f;
            background.outlineColor = themeAsset.GetColor(NowColorToken.Border, Color.gray);

            if (interaction.hovered)
                background.color = NowControls.StateTint(background.color, 1f, interaction.held);

            background.Draw();

            string label = showCopied ? "Copied!" : "Copy";
            var text = themeAsset.Text(default(NowRect), _layoutFont);
            text.fontSize = _style.fontSize * 0.75f;
            Color labelColor = showCopied
                ? themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                : themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
            text.color = labelColor;

            Vector2 size = text.Measure(label);
            text.rect = new NowRect(
                target.x + (target.width - size.x) * 0.5f,
                target.y + (target.height - size.y) * 0.5f,
                size.x + 1f,
                size.y + 1f);
            text.SetMask(target.Outset(4f)).Draw(label);
            return clicked;
        }

        static Vector4 ResolveColor(NowThemeAsset themeAsset, Role role, bool hovered)
        {
            switch (role)
            {
                case Role.Heading:
                case Role.Body:
                    return themeAsset.GetColor(NowColorToken.Text, Color.black);
                case Role.Code:
                    return themeAsset.GetColor(NowColorToken.Text, Color.black);
                case Role.CodePanel:
                case Role.TableHeaderFill:
                    return themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f));
                case Role.Link:
                {
                    Vector4 accent = themeAsset.GetColor(NowColorToken.Accent, Color.blue);

                    if (hovered)
                    {
                        accent.x *= 1.2f;
                        accent.y *= 1.2f;
                        accent.z *= 1.2f;
                    }

                    return accent;
                }
                case Role.Muted:
                case Role.SyntaxComment:
                    return themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
                case Role.SyntaxKeyword:
                    return themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                case Role.SyntaxString:
                    return new Vector4(0.16f, 0.52f, 0.26f, 1f);
                case Role.SyntaxNumber:
                    return new Vector4(0.55f, 0.27f, 0.68f, 1f);
                case Role.Rule:
                case Role.TableLine:
                case Role.CheckBox:
                    return themeAsset.GetColor(NowColorToken.Border, new Color(0.886f, 0.910f, 0.941f, 1f));
                case Role.QuoteBar:
                case Role.Bullet:
                case Role.CheckFill:
                    return themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                default:
                    return themeAsset.GetColor(NowColorToken.Text, Color.black);
            }
        }

        void EnsureLayout(float width)
        {
            var theme = NowTheme.themeAsset;
            var probe = theme.Text(default(NowRect), (string)null);
            NowFontAsset font = probe.font;

            if (font == _layoutFont && Mathf.Abs(width - _layoutWidth) <= 0.5f &&
                _imagesVersion == NowMarkdownImages.version)
            {
                return;
            }

            _layoutWidth = width;
            _layoutFont = font;
            _imagesVersion = NowMarkdownImages.version;
            _hasLoadingImages = false;
            _strikeSequence = 0;
            _ops.Clear();
            _links.Clear();
            _selectionRegions.Clear();
            _selectionLines.Clear();

            if (font == null || width <= 1f)
            {
                _layoutHeight = 0f;
                return;
            }

            float y = 0f;
            LayoutChildren(_root, 0f, ref y, width, 0);
            _layoutHeight = y;
            BuildDocumentText();
        }

        /// <summary>
        /// Joins every region's text into one document string (blocks separated
        /// by blank lines) and rebases segment ranges onto it, so one selection
        /// can span the whole document like dragging over a webpage.
        /// </summary>
        void BuildDocumentText()
        {
            _regionBuilder.Length = 0;

            for (int r = 0; r < _selectionRegions.Count; ++r)
            {
                if (r > 0)
                    _regionBuilder.Append('\n').Append('\n');

                int baseOffset = _regionBuilder.Length;
                var region = _selectionRegions[r];

                for (int l = 0; l < region.lineCount; ++l)
                {
                    var line = _selectionLines[region.lineStart + l];
                    line.start += baseOffset;
                    _selectionLines[region.lineStart + l] = line;
                }

                _regionBuilder.Append(region.literal);
            }

            _documentText = _regionBuilder.ToString();
        }

        float LineHeight(float fontSize)
        {
            return _layoutFont.GetLineHeight() * fontSize;
        }

        float BlockSpacing => _style.fontSize * 0.65f;

        void LayoutChildren(NowMarkdownBlock parent, float x, ref float y, float width, int depth)
        {
            for (int i = 0; i < parent.children.Count; ++i)
            {
                if (i > 0)
                    y += BlockSpacing;

                LayoutBlock(parent.children[i], x, ref y, width, depth);
            }
        }

        void LayoutBlock(NowMarkdownBlock block, float x, ref float y, float width, int depth)
        {
            switch (block.type)
            {
                case NowMarkdownBlockType.Heading:
                {
                    float scale = block.level switch
                    {
                        1 => 1.9f,
                        2 => 1.55f,
                        3 => 1.3f,
                        4 => 1.15f,
                        5 => 1f,
                        _ => 0.9f
                    };
                    float size = _style.fontSize * scale;
                    LayoutInlines(block.inlines, x, ref y, width, size, NowFontStyle.Bold, Role.Heading, -1);

                    if (block.level <= 2)
                    {
                        y += size * 0.3f;
                        AddFill(Role.Rule, new NowRect(x, y, width - x, 1f));
                        y += 1f;
                    }

                    break;
                }
                case NowMarkdownBlockType.Paragraph:
                    LayoutInlines(block.inlines, x, ref y, width, _style.fontSize, NowFontStyle.Regular, Role.Body, -1);
                    break;
                case NowMarkdownBlockType.CodeBlock:
                    LayoutCodeBlock(block, x, ref y, width);
                    break;
                case NowMarkdownBlockType.Quote:
                {
                    float top = y;
                    float inset = _style.fontSize * 0.9f;
                    LayoutChildren(block, x + inset, ref y, width, depth);
                    AddFill(Role.QuoteBar, new NowRect(x, top, 3f, y - top));
                    break;
                }
                case NowMarkdownBlockType.List:
                    LayoutList(block, x, ref y, width, depth);
                    break;
                case NowMarkdownBlockType.ThematicBreak:
                    y += _style.fontSize * 0.25f;
                    AddFill(Role.Rule, new NowRect(x, y, width - x, 1.5f));
                    y += 1.5f + _style.fontSize * 0.25f;
                    break;
                case NowMarkdownBlockType.Table:
                    LayoutTable(block, x, ref y, width);
                    break;
            }
        }

        static readonly List<NowTextToken> _tokenScratch = new List<NowTextToken>(16);

        void LayoutCodeBlock(NowMarkdownBlock block, float x, ref float y, float width)
        {
            float pad = _style.fontSize * 0.6f;
            float size = _style.fontSize * 0.92f;
            float lineHeight = LineHeight(size);
            float top = y;
            int panelIndex = _ops.Count;
            AddFill(Role.CodePanel, default);

            _ops.Add(new Op
            {
                kind = OpKind.SelectionLayer,
                role = Role.Body,
                link = _selectionRegions.Count
            });
            int regionLineStart = _selectionLines.Count;

            y += pad;
            string literal = block.literal ?? string.Empty;
            var language = NowMarkdownSyntax.GetLanguage(block.info);
            var syntaxState = default(NowMarkdownSyntaxState);
            int lineStart = 0;

            for (int i = 0; i <= literal.Length; ++i)
            {
                if (i != literal.Length && literal[i] != '\n')
                    continue;

                _selectionLines.Add(new NowTextSelectionLine
                {
                    rect = new NowRect(x + pad, y, width - x - pad * 2f, lineHeight),
                    start = lineStart,
                    length = i - lineStart,
                    fontSize = size
                });

                string line = literal.Substring(lineStart, i - lineStart);

                if (line.IndexOf('\t') >= 0)
                    line = line.Replace("\t", "    ");

                if (line.Length > 0)
                {
                    NowMarkdownSyntax.TokenizeLine(line, language, ref syntaxState, _tokenScratch);
                    float cx = x + pad;

                    for (int t = 0; t < _tokenScratch.Count; ++t)
                    {
                        var token = _tokenScratch[t];
                        string segment = line.Substring(token.start, token.length);
                        float segmentWidth = _layoutFont.MeasureText(segment, size).x;

                        AddText(segment, new NowRect(cx, y, segmentWidth + 1f, lineHeight), size,
                            NowFontStyle.Regular, TokenRole(token.kind), -1);
                        cx += segmentWidth;
                    }
                }

                y += lineHeight;
                lineStart = i + 1;
            }

            y += pad;

            float buttonWidth = _style.fontSize * 3.6f;
            float buttonHeight = _style.fontSize * 1.5f;
            var buttonRect = new NowRect(width - 6f - buttonWidth, top + 6f, buttonWidth, buttonHeight);

            _selectionRegions.Add(new SelectionRegion
            {
                literal = literal,
                lineStart = regionLineStart,
                lineCount = _selectionLines.Count - regionLineStart,
                fontSize = size,
                buttonRect = buttonRect
            });

            var panel = _ops[panelIndex];
            panel.rect = new NowRect(x, top, width - x, y - top);
            _ops[panelIndex] = panel;

            _ops.Add(new Op
            {
                kind = OpKind.CopyButton,
                role = Role.Body,
                rect = buttonRect,
                hoverRect = panel.rect,
                text = literal,
                link = -1
            });
        }

        static Role TokenRole(NowMarkdownTokenKind kind)
        {
            switch (kind)
            {
                case NowMarkdownTokenKind.Keyword: return Role.SyntaxKeyword;
                case NowMarkdownTokenKind.String: return Role.SyntaxString;
                case NowMarkdownTokenKind.Number: return Role.SyntaxNumber;
                case NowMarkdownTokenKind.Comment: return Role.SyntaxComment;
                default: return Role.Code;
            }
        }

        static Role TokenRole(NowTextTokenKind kind)
        {
            switch (kind)
            {
                case NowTextTokenKind.Keyword: return Role.SyntaxKeyword;
                case NowTextTokenKind.String: return Role.SyntaxString;
                case NowTextTokenKind.Number: return Role.SyntaxNumber;
                case NowTextTokenKind.Comment: return Role.SyntaxComment;
                default: return Role.Code;
            }
        }

        void LayoutList(NowMarkdownBlock list, float x, ref float y, float width, int depth)
        {
            float markerWidth = _style.fontSize * (list.ordered ? 1.6f : 1.2f);
            int number = list.start;

            for (int i = 0; i < list.children.Count; ++i)
            {
                if (i > 0)
                    y += BlockSpacing * 0.5f;

                var item = list.children[i];
                float lineHeight = LineHeight(_style.fontSize);

                if (item.isTask)
                {
                    float box = _style.fontSize * 0.85f;
                    float boxY = y + (lineHeight - box) * 0.5f;
                    AddFill(Role.CheckBox, new NowRect(x + 1f, boxY, box, box));

                    if (item.isChecked)
                    {
                        float inset = box * 0.25f;
                        AddFill(Role.CheckFill, new NowRect(x + 1f + inset, boxY + inset, box - inset * 2f, box - inset * 2f));
                    }
                }
                else if (list.ordered)
                {
                    AddText(number + ".", new NowRect(x, y, markerWidth, lineHeight), _style.fontSize,
                        NowFontStyle.Regular, Role.Muted, -1);
                }
                else
                {
                    float dot = _style.fontSize * 0.32f;
                    AddFill(Role.Bullet, new NowRect(x + dot, y + (lineHeight - dot) * 0.5f, dot, dot));
                }

                ++number;
                LayoutChildren(item, x + markerWidth, ref y, width, depth + 1);
            }
        }

        void LayoutTable(NowMarkdownBlock table, float x, ref float y, float width)
        {
            int columns = table.tableAligns.Count;
            int rows = table.tableRows.Count;
            float cellPad = _style.fontSize * 0.45f;
            float lineHeight = LineHeight(_style.fontSize);

            Span<float> stackBuffer = stackalloc float[16];
            Span<float> stackWidths = columns <= 16 ? stackBuffer.Slice(0, columns) : new float[columns];

            for (int r = 0; r < rows; ++r)
            {
                for (int c = 0; c < columns; ++c)
                {
                    float w = MeasureInlines(table.tableRows[r][c], _style.fontSize,
                        r == 0 ? NowFontStyle.Bold : NowFontStyle.Regular);

                    if (w > stackWidths[c])
                        stackWidths[c] = w;
                }
            }

            float total = 0f;

            for (int c = 0; c < columns; ++c)
            {
                stackWidths[c] += cellPad * 2f;
                total += stackWidths[c];
            }

            float available = width - x;

            if (total > available && total > 0f)
            {
                float shrink = available / total;

                for (int c = 0; c < columns; ++c)
                    stackWidths[c] *= shrink;

                total = available;
            }

            for (int r = 0; r < rows; ++r)
            {
                float rowTop = y;
                float rowHeight = lineHeight + cellPad;

                if (r == 0)
                    AddFill(Role.TableHeaderFill, new NowRect(x, rowTop, total, rowHeight));

                float cx = x;

                for (int c = 0; c < columns; ++c)
                {
                    var cell = table.tableRows[r][c];
                    float cellWidth = stackWidths[c];
                    float textWidth = MeasureInlines(cell, _style.fontSize, r == 0 ? NowFontStyle.Bold : NowFontStyle.Regular);
                    float tx = cx + cellPad;

                    var align = table.tableAligns[c];

                    if (align == NowMarkdownAlign.Center)
                        tx = cx + (cellWidth - textWidth) * 0.5f;
                    else if (align == NowMarkdownAlign.Right)
                        tx = cx + cellWidth - cellPad - textWidth;

                    float cellY = rowTop + cellPad * 0.5f;
                    LayoutInlineRow(cell, tx, cellY, cx + cellWidth - cellPad, _style.fontSize,
                        r == 0 ? NowFontStyle.Bold : NowFontStyle.Regular, Role.Body);
                    cx += cellWidth;
                }

                y = rowTop + rowHeight;
                AddFill(Role.TableLine, new NowRect(x, y, total, 1f));
            }
        }

        void LayoutInlines(List<NowMarkdownInline> inlines, float x, ref float y, float width,
            float fontSize, NowFontStyle baseStyle, Role baseRole, int link)
        {
            var cursor = new NowRichTextCursor
            {
                x = x,
                y = y,
                lineStart = x,
                limit = width,
                lineHeight = LineHeight(fontSize)
            };

            int selectionOpIndex = _ops.Count;
            _ops.Add(new Op { kind = OpKind.SelectionLayer, role = Role.Body, link = _selectionRegions.Count });

            _regionActive = true;
            _regionSegmentStart = _selectionLines.Count;
            _richText.Clear();

            int opStart = _ops.Count;
            LayoutInlineNodes(inlines, ref cursor, fontSize, baseStyle, baseRole, link, 0, true);
            MergeDecorations(opStart);
            _richText.CompleteLines();

            _regionActive = false;

            for (int i = 0; i < _richText.selectionLines.Count; ++i)
                _selectionLines.Add(_richText.selectionLines[i]);

            if (_richText.selectionLines.Count > 0)
            {
                _selectionRegions.Add(new SelectionRegion
                {
                    literal = _richText.text,
                    lineStart = _regionSegmentStart,
                    lineCount = _richText.selectionLines.Count,
                    fontSize = fontSize
                });
            }
            else
            {
                var inert = _ops[selectionOpIndex];
                inert.link = -1;
                _ops[selectionOpIndex] = inert;
            }

            y = cursor.y + cursor.lineHeight;
        }

        void LayoutInlineRow(List<NowMarkdownInline> inlines, float x, float y, float limit,
            float fontSize, NowFontStyle baseStyle, Role baseRole)
        {
            var cursor = new NowRichTextCursor
            {
                x = x,
                y = y,
                lineStart = x,
                limit = limit,
                lineHeight = LineHeight(fontSize)
            };

            int opStart = _ops.Count;
            LayoutInlineNodes(inlines, ref cursor, fontSize, baseStyle, baseRole, -1, 0, false);
            MergeDecorations(opStart);
        }

        void LayoutInlineNodes(List<NowMarkdownInline> nodes, ref NowRichTextCursor cursor, float fontSize,
            NowFontStyle style, Role role, int link, int strike, bool wrap)
        {
            for (int i = 0; i < nodes.Count; ++i)
            {
                var node = nodes[i];

                switch (node.type)
                {
                    case NowMarkdownInlineType.Text:
                        LayoutWords(node.text, ref cursor, fontSize, style, role, link, strike, wrap);
                        break;
                    case NowMarkdownInlineType.Code:
                        LayoutCodeSpan(node.text, ref cursor, fontSize, link, wrap);
                        break;
                    case NowMarkdownInlineType.Emphasis:
                        LayoutInlineNodes(node.children, ref cursor, fontSize, style | NowFontStyle.Italic, role, link, strike, wrap);
                        break;
                    case NowMarkdownInlineType.Strong:
                        LayoutInlineNodes(node.children, ref cursor, fontSize, style | NowFontStyle.Bold, role, link, strike, wrap);
                        break;
                    case NowMarkdownInlineType.Strikethrough:
                        LayoutInlineNodes(node.children, ref cursor, fontSize, style, role, link, --_strikeSequence, wrap);
                        break;
                    case NowMarkdownInlineType.Link:
                    {
                        _links.Add(node.url ?? string.Empty);
                        LayoutInlineNodes(node.children, ref cursor, fontSize, style, Role.Link, _links.Count - 1, strike, wrap);
                        break;
                    }
                    case NowMarkdownInlineType.Image:
                        LayoutImage(node, ref cursor, fontSize, style, link);
                        break;
                    case NowMarkdownInlineType.HardBreak:
                        NowRichTextFlow.NewLine(ref cursor);
                        break;
                    case NowMarkdownInlineType.SoftBreak:
                        LayoutWords(" ", ref cursor, fontSize, style, role, link, strike, wrap);
                        break;
                }
            }
        }

        void LayoutWords(string text, ref NowRichTextCursor cursor, float fontSize, NowFontStyle style,
            Role role, int link, int strike, bool wrap)
        {
            if (string.IsNullOrEmpty(text))
                return;

            float spaceWidth = _layoutFont.MeasureText(" ", fontSize, style).x;
            int i = 0;

            while (i < text.Length)
            {
                if (text[i] == ' ')
                {
                    if (cursor.x > cursor.lineStart)
                        cursor.x += spaceWidth;

                    ++i;
                    continue;
                }

                int wordStart = i;

                while (i < text.Length && text[i] != ' ')
                    ++i;

                string word = text.Substring(wordStart, i - wordStart);
                var rect = _regionActive
                    ? _richText.AddWord(ref cursor, word, _layoutFont, fontSize, style, wrap)
                    : NowRichTextFlow.AddWord(ref cursor, word, 0, word.Length, _layoutFont, fontSize, style, wrap);
                float wordWidth = rect.width - 1f;
                AddText(word, rect, fontSize, style, role, link);

                if (link >= 0)
                    AddLine(Role.Link, new NowRect(rect.x, rect.y + cursor.lineHeight - 2f, wordWidth, 1f), link);

                if (strike != 0)
                    AddLine(Role.Body, new NowRect(rect.x, rect.y + cursor.lineHeight * 0.55f, wordWidth, 1f), strike);

            }
        }

        void LayoutImage(NowMarkdownInline node, ref NowRichTextCursor cursor, float fontSize, NowFontStyle style, int link)
        {
            var state = NowMarkdownImages.GetState(node.url, out var texture);

            if (state == NowMarkdownImageState.Loaded && texture != null)
            {
                if (cursor.x > cursor.lineStart)
                    NowRichTextFlow.NewLine(ref cursor);

                float maxWidth = Mathf.Max(cursor.limit - cursor.lineStart, 16f);
                float drawWidth = Mathf.Min(texture.width, maxWidth);
                float drawHeight = texture.height * (drawWidth / Mathf.Max(texture.width, 1f));

                _ops.Add(new Op
                {
                    kind = OpKind.Image,
                    role = Role.Body,
                    rect = new NowRect(cursor.lineStart, cursor.y, drawWidth, drawHeight),
                    text = node.url,
                    link = link
                });

                cursor.x = cursor.lineStart;
                cursor.y += drawHeight;
                ++cursor.lineIndex;
                return;
            }

            if (state == NowMarkdownImageState.Loading)
            {
                _hasLoadingImages = true;

                if (cursor.x > cursor.lineStart)
                    NowRichTextFlow.NewLine(ref cursor);

                float panelHeight = cursor.lineHeight * 2f;
                AddFill(Role.CodePanel, new NowRect(cursor.lineStart, cursor.y, cursor.limit - cursor.lineStart, panelHeight));

                var inner = cursor;
                inner.x = cursor.lineStart + fontSize * 0.5f;
                inner.y = cursor.y + (panelHeight - cursor.lineHeight) * 0.5f;
                LayoutInlineNodes(node.children, ref inner, fontSize, style, Role.Muted, -1, 0, false);

                cursor.x = cursor.lineStart;
                cursor.y += panelHeight;
                ++cursor.lineIndex;
                return;
            }

            LayoutInlineNodes(node.children, ref cursor, fontSize, style, Role.Muted, link, 0, true);
        }

        void LayoutCodeSpan(string code, ref NowRichTextCursor cursor, float fontSize, int link, bool wrap)
        {
            if (string.IsNullOrEmpty(code))
                return;

            float size = fontSize * 0.92f;
            float pad = fontSize * 0.25f;
            float textWidth = _layoutFont.MeasureText(code, size).x;
            float total = textWidth + pad * 2f;

            NowRichTextFlow.WrapBefore(ref cursor, total, wrap);

            AddFill(Role.CodePanel, new NowRect(cursor.x, cursor.y + 1f, total, cursor.lineHeight - 2f));
            var codeRect = new NowRect(cursor.x + pad, cursor.y, textWidth + 1f, cursor.lineHeight);
            AddText(code, codeRect, size, NowFontStyle.Regular, Role.Code, link);

            if (_regionActive)
                _richText.AddPlacedRun(code, codeRect, new NowRichTextStyle(size, NowFontStyle.Regular),
                    cursor.lineIndex, font: _layoutFont);

            cursor.x += total;
        }

        float MeasureInlines(List<NowMarkdownInline> nodes, float fontSize, NowFontStyle style)
        {
            float width = 0f;

            for (int i = 0; i < nodes.Count; ++i)
            {
                var node = nodes[i];

                switch (node.type)
                {
                    case NowMarkdownInlineType.Text:
                        width += _layoutFont.MeasureText(node.text, fontSize, style).x;
                        break;
                    case NowMarkdownInlineType.Code:
                        width += _layoutFont.MeasureText(node.text, fontSize * 0.92f).x + fontSize * 0.5f;
                        break;
                    case NowMarkdownInlineType.Emphasis:
                        width += MeasureInlines(node.children, fontSize, style | NowFontStyle.Italic);
                        break;
                    case NowMarkdownInlineType.Strong:
                        width += MeasureInlines(node.children, fontSize, style | NowFontStyle.Bold);
                        break;
                    case NowMarkdownInlineType.Strikethrough:
                    case NowMarkdownInlineType.Link:
                        width += MeasureInlines(node.children, fontSize, style);
                        break;
                    case NowMarkdownInlineType.SoftBreak:
                    case NowMarkdownInlineType.HardBreak:
                        width += _layoutFont.MeasureText(" ", fontSize, style).x;
                        break;
                }
            }

            return width;
        }

        void AddText(string text, NowRect rect, float fontSize, NowFontStyle style, Role role, int link)
        {
            _ops.Add(new Op
            {
                kind = OpKind.Text,
                role = role,
                rect = rect,
                text = text,
                fontSize = fontSize,
                fontStyle = style,
                link = link
            });
        }

        void AddFill(Role role, NowRect rect)
        {
            _ops.Add(new Op { kind = OpKind.Fill, role = role, rect = rect, link = -1 });
        }

        void AddLine(Role role, NowRect rect, int link)
        {
            _ops.Add(new Op { kind = OpKind.Line, role = role, rect = rect, link = link });
        }

        /// <summary>
        /// Per-word underline/strike segments on the same line merge into one
        /// continuous decoration, bridging the spaces — a multi-word link reads
        /// as one link, not one per word. Strike ops carry per-instance negative
        /// tokens so separate strikethroughs never bridge. Decoration ops with
        /// negative link tokens never interact, so only underlines (link >= 0)
        /// affect hover visuals.
        /// </summary>
        void MergeDecorations(int fromOp)
        {
            float maxGap = _style.fontSize * 0.8f;

            for (int i = fromOp; i < _ops.Count; ++i)
            {
                if (_ops[i].kind != OpKind.Line)
                    continue;

                for (int j = i + 1; j < _ops.Count; ++j)
                {
                    if (_ops[j].kind != OpKind.Line)
                        continue;

                    var first = _ops[i];
                    var second = _ops[j];

                    if (second.link != first.link || second.role != first.role ||
                        Mathf.Abs(second.rect.y - first.rect.y) > 0.5f)
                    {
                        continue;
                    }

                    float gap = second.rect.x - (first.rect.x + first.rect.width);

                    if (gap < -0.5f || gap > maxGap)
                        continue;

                    first.rect = new NowRect(
                        first.rect.x,
                        first.rect.y,
                        second.rect.x + second.rect.width - first.rect.x,
                        first.rect.height);
                    _ops[i] = first;
                    _ops.RemoveAt(j);
                    --j;
                }
            }
        }
    }
}
