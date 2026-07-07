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

    public struct NowTextDiagnostic
    {
        public int start;

        public int length;

        public string message;
    }

    public interface INowTextSyntaxProfile
    {
        string name { get; }

        int TokenizeLine(string text, int start, int length, int state, List<NowTextToken> tokens);

        void Validate(string text, List<NowTextDiagnostic> diagnostics);
    }
}
