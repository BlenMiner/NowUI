using System;
using System.Collections.Generic;
using System.Text;

namespace NowUI.Markup
{
    /// <summary>
    /// Everything a parsed document declares that C# code may look up by name:
    /// element ids (plus derived ids such as gallery prev/next buttons), state
    /// keys, and <c>emit(...)</c> action names. Built once at parse time so
    /// result queries can be validated and so <see cref="NowMarkupBindings"/>
    /// can generate constants instead of hand-written strings.
    /// </summary>
    public sealed class NowMarkupManifest
    {
        static readonly string[] ActionAttributes = { "on-click", "onclick", "on-change", "onchange", "action" };

        static readonly string[] StateAttributes = { "state", "bind", "value-key" };

        readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _actions = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _idList = new List<string>();
        readonly List<string> _keyList = new List<string>();
        readonly List<string> _actionList = new List<string>();

        /// <summary>Explicit element ids, plus ids derived from them (gallery ".prev"/".next").</summary>
        public IReadOnlyList<string> ids => _idList;

        /// <summary>State keys the document reads or writes: bindings, groups, gallery/tab indices.</summary>
        public IReadOnlyList<string> keys => _keyList;

        /// <summary>Names emitted through <c>emit(...)</c> actions.</summary>
        public IReadOnlyList<string> actions => _actionList;

        public bool DeclaresId(string id)
        {
            return id != null && _ids.Contains(id);
        }

        public bool DeclaresKey(string key)
        {
            return key != null && _keys.Contains(key);
        }

        public bool DeclaresAction(string name)
        {
            return name != null && _actions.Contains(name);
        }

        internal static NowMarkupManifest FromDocument(NowMarkupNode root)
        {
            var manifest = new NowMarkupManifest();
            manifest.Collect(root);
            return manifest;
        }

        void Collect(NowMarkupNode node)
        {
            if (node == null || node.isText)
                return;

            if (node.name != "document")
                CollectElement(node);

            for (int i = 0; i < node.children.Count; ++i)
                Collect(node.children[i]);
        }

        void CollectElement(NowMarkupNode node)
        {
            string id = Attribute(node, "id") ?? Attribute(node, "name");

            if (id != null)
            {
                AddId(id);

                if (node.name == "gallery")
                {
                    AddId(id + ".prev");
                    AddId(id + ".next");
                }
            }

            string key = Attribute(node, StateAttributes);

            if (node.name == "radio")
                key = Attribute(node, "group") ?? key;
            else if (node.name == "gallery" || node.name == "tabs" || node.name == "tabview")
                key = Attribute(node, "index") ?? key;

            if (key != null)
                AddKey(key);
            else if (id != null && IsStateful(node.name))
                AddKey(UsesIndexKey(node.name) ? id + ".index" : id);

            for (int i = 0; i < ActionAttributes.Length; ++i)
            {
                if (node.TryAttribute(ActionAttributes[i], out var script))
                    CollectEmits(script);
            }
        }

        void CollectEmits(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return;

            int cursor = 0;

            while (cursor < script.Length)
            {
                int semi = script.IndexOf(';', cursor);
                int end = semi >= 0 ? semi : script.Length;
                string command = script.Substring(cursor, end - cursor).Trim();

                if (command.StartsWith("emit", StringComparison.OrdinalIgnoreCase))
                {
                    string args = command.Substring(4).Trim();

                    if (args.StartsWith("(", StringComparison.Ordinal) && args.EndsWith(")", StringComparison.Ordinal))
                        args = args.Substring(1, args.Length - 2);
                    else if (args.StartsWith(":", StringComparison.Ordinal))
                        args = args.Substring(1);
                    else
                        args = string.Empty;

                    int comma = args.IndexOf(',');
                    string name = (comma >= 0 ? args.Substring(0, comma) : args).Trim().Trim('"', '\'');

                    if (name.Length > 0)
                        AddAction(name);
                }

                if (semi < 0)
                    break;

                cursor = semi + 1;
            }
        }

        void AddId(string id)
        {
            if (_ids.Add(id))
                _idList.Add(id);
        }

        void AddKey(string key)
        {
            if (_keys.Add(key))
                _keyList.Add(key);
        }

        void AddAction(string name)
        {
            if (_actions.Add(name))
                _actionList.Add(name);
        }

        static bool UsesIndexKey(string tag)
        {
            return tag == "gallery" || tag == "tabs" || tag == "tabview";
        }

        static bool IsStateful(string tag)
        {
            switch (tag)
            {
                case "checkbox":
                case "switch":
                case "toggle":
                case "slider":
                case "textfield":
                case "input":
                case "textarea":
                case "dropdown":
                case "select":
                case "radio":
                case "gallery":
                case "tabs":
                case "tabview":
                case "details":
                case "chip":
                case "progress":
                case "progressbar":
                    return true;
                default:
                    return false;
            }
        }

        static string Attribute(NowMarkupNode node, string name)
        {
            return node.TryAttribute(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
        }

        static string Attribute(NowMarkupNode node, string[] names)
        {
            for (int i = 0; i < names.Length; ++i)
            {
                string value = Attribute(node, names[i]);

                if (value != null)
                    return value;
            }

            return null;
        }
    }

    /// <summary>
    /// Generates a C# constants class from a document's manifest so game code
    /// references <c>MainUi.Ids.Save</c> instead of repeating the string
    /// <c>"save"</c> at every call site. The markup file stays the single
    /// source of truth; regenerate after renaming ids.
    /// </summary>
    public static class NowMarkupBindings
    {
        public static string GenerateSource(NowMarkupDocument document, string className, string @namespace = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            className = SanitizeIdentifier(className, "MarkupBindings");
            var manifest = document.manifest;
            var builder = new StringBuilder(1024);
            string indent = string.Empty;

            builder.AppendLine("// <auto-generated>");
            builder.AppendLine("//     Generated by NowUI.Markup.NowMarkupBindings. Do not edit by hand;");
            builder.AppendLine("//     regenerate after changing the markup source.");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                builder.Append("namespace ").AppendLine(@namespace.Trim());
                builder.AppendLine("{");
                indent = "    ";
            }

            builder.Append(indent).Append("public static class ").AppendLine(className);
            builder.Append(indent).AppendLine("{");
            AppendGroup(builder, indent, "Ids", manifest.ids);
            builder.AppendLine();
            AppendGroup(builder, indent, "Keys", manifest.keys);
            builder.AppendLine();
            AppendGroup(builder, indent, "Actions", manifest.actions);
            builder.Append(indent).AppendLine("}");

            if (indent.Length > 0)
                builder.AppendLine("}");

            return builder.ToString();
        }

        static void AppendGroup(StringBuilder builder, string indent, string groupName, IReadOnlyList<string> values)
        {
            builder.Append(indent).Append("    public static class ").AppendLine(groupName);
            builder.Append(indent).AppendLine("    {");

            var used = new HashSet<string>(StringComparer.Ordinal);
            var sorted = new List<string>(values);
            sorted.Sort(StringComparer.Ordinal);

            for (int i = 0; i < sorted.Count; ++i)
            {
                string identifier = SanitizeIdentifier(sorted[i], "Item");
                string unique = identifier;
                int suffix = 2;

                while (!used.Add(unique))
                    unique = identifier + suffix++;

                builder.Append(indent)
                    .Append("        public const string ")
                    .Append(unique)
                    .Append(" = \"")
                    .Append(Escape(sorted[i]))
                    .AppendLine("\";");
            }

            builder.Append(indent).AppendLine("    }");
        }

        static string SanitizeIdentifier(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var builder = new StringBuilder(value.Length);
            bool upperNext = true;

            for (int i = 0; i < value.Length; ++i)
            {
                char c = value[i];

                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(upperNext && char.IsLetter(c) ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                    continue;
                }

                upperNext = true;
            }

            if (builder.Length == 0)
                return fallback;

            if (char.IsDigit(builder[0]))
                builder.Insert(0, '_');

            return builder.ToString();
        }

        static string Escape(string value)
        {
            if (value.IndexOf('"') < 0 && value.IndexOf('\\') < 0)
                return value;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
