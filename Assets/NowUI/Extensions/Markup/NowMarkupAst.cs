using System.Collections.Generic;

namespace NowUI.Markup
{
    public enum NowMarkupNodeType
    {
        Element,
        Text
    }

    public sealed class NowMarkupNode
    {
        public NowMarkupNodeType type;
        public string name;
        public string text;
        public int sourceIndex;
        public readonly Dictionary<string, string> attributes = new Dictionary<string, string>();
        public readonly List<NowMarkupNode> children = new List<NowMarkupNode>();

        /// <summary>Per-node render data resolved once by the owning document.</summary>
        internal NowMarkupNodeCache renderCache;

        public bool isText => type == NowMarkupNodeType.Text;

        public string Attribute(string key, string fallback = "")
        {
            return attributes.TryGetValue(key, out var value) ? value : fallback;
        }

        public bool TryAttribute(string key, out string value)
        {
            return attributes.TryGetValue(key, out value);
        }

        public bool HasAttribute(string key)
        {
            return attributes.ContainsKey(key);
        }
    }
}
