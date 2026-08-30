using System.Collections.Generic;

namespace NowUI
{
    public enum NowTextTokenKind : byte
    {
        Plain,
        Keyword,
        String,
        Number,
        Comment,
        Punctuation,
        Property,
        Error,
        Heading,
        Strong,
        Emphasis,
        CodeSpan,
        Link,
        Quote,
        ListMarker,
        Fence,
        Tag,
        Attribute,
        Constant,
        DocComment,
        DocTag
    }

    public struct NowTextToken
    {
        public int start;

        public int length;

        public NowTextTokenKind kind;
    }

    /// <summary>
    /// Mirror of the code editor's diagnostic severity, castable by value —
    /// the same arrangement <see cref="NowTextTokenKind"/> has with the code
    /// editor's token kinds. Error is zero so an unset severity reads as one.
    /// </summary>
    public enum NowTextDiagnosticSeverity : byte
    {
        Error,
        Warning,
        Info
    }

    public struct NowTextDiagnostic
    {
        public int start;

        public int length;

        public string message;

        public NowTextDiagnosticSeverity severity;
    }

    public interface INowTextSyntaxProfile
    {
        string name { get; }

        int TokenizeLine(string text, int start, int length, int state, List<NowTextToken> tokens);

        void Validate(string text, List<NowTextDiagnostic> diagnostics);
    }
}
