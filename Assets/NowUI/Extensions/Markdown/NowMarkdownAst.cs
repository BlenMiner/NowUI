using System.Collections.Generic;

namespace NowUI.Markdown
{
    public enum NowMarkdownBlockType
    {
        Document,
        Heading,
        Paragraph,
        CodeBlock,
        Quote,
        List,
        ListItem,
        ThematicBreak,
        Table
    }

    public enum NowMarkdownInlineType
    {
        Text,
        Emphasis,
        Strong,
        Strikethrough,
        Code,
        Link,
        HardBreak,
        SoftBreak
    }

    public enum NowMarkdownAlign
    {
        None,
        Left,
        Center,
        Right
    }

    /// <summary>
    /// A block node. Parse-time allocations only — documents are parsed once and
    /// drawn for many frames.
    /// </summary>
    public sealed class NowMarkdownBlock
    {
        public NowMarkdownBlockType type;

        /// <summary>Heading level 1-6.</summary>
        public int level;

        /// <summary>List: ordered vs bullet, and the first item number.</summary>
        public bool ordered;

        public int start = 1;

        /// <summary>List item: task checkbox state ("- [ ]" / "- [x]").</summary>
        public bool isTask;

        public bool isChecked;

        /// <summary>Code block: verbatim content and the fence info string.</summary>
        public string literal;

        public string info;

        public readonly List<NowMarkdownBlock> children = new List<NowMarkdownBlock>();

        /// <summary>Heading/paragraph content.</summary>
        public List<NowMarkdownInline> inlines;

        /// <summary>Table content: rows of cells, each cell an inline list. Row 0 is the header.</summary>
        public List<List<List<NowMarkdownInline>>> tableRows;

        public List<NowMarkdownAlign> tableAligns;
    }

    /// <summary>An inline node inside a heading, paragraph, list item or table cell.</summary>
    public sealed class NowMarkdownInline
    {
        public NowMarkdownInlineType type;

        /// <summary>Literal content for Text and Code nodes.</summary>
        public string text;

        /// <summary>Destination for Link nodes.</summary>
        public string url;

        /// <summary>Styled children for Emphasis/Strong/Strikethrough/Link nodes.</summary>
        public List<NowMarkdownInline> children;

        public static NowMarkdownInline Leaf(NowMarkdownInlineType type, string text = null)
        {
            return new NowMarkdownInline { type = type, text = text };
        }

        public static NowMarkdownInline Container(NowMarkdownInlineType type)
        {
            return new NowMarkdownInline { type = type, children = new List<NowMarkdownInline>() };
        }
    }
}
