using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NowUI.Markup.Editor
{
    /// <summary>
    /// Project-window menu that turns a markup file's declared ids, state keys,
    /// and action names into a generated C# constants class, so game code
    /// references <c>MainMarkup.Ids.Save</c> instead of repeating lookup
    /// strings by hand.
    /// </summary>
    public static class NowMarkupBindingsGenerator
    {
        const string MenuPath = "Assets/NowUI/Generate Markup Bindings";

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            return TryGetMarkupPath(out _);
        }

        [MenuItem(MenuPath)]
        static void Generate()
        {
            if (!TryGetMarkupPath(out string path))
                return;

            var document = NowMarkupDocument.Parse(File.ReadAllText(path));
            string className = ClassName(path);
            string source = NowMarkupBindings.GenerateSource(document, className);
            string target = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, className + ".cs");

            File.WriteAllText(target, source);
            AssetDatabase.Refresh();
            Debug.Log($"NowMarkup: generated {target} from {path}.");
        }

        static bool TryGetMarkupPath(out string path)
        {
            path = Selection.activeObject != null
                ? AssetDatabase.GetAssetPath(Selection.activeObject)
                : null;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".nowui":
                case ".markup":
                case ".xml":
                    return true;
                default:
                    return false;
            }
        }

        static string ClassName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var builder = new StringBuilder(name.Length + 8);
            bool upper = true;

            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(upper && char.IsLetter(c) ? char.ToUpperInvariant(c) : c);
                    upper = false;
                    continue;
                }

                upper = true;
            }

            if (builder.Length == 0 || char.IsDigit(builder[0]))
                builder.Insert(0, "Generated");

            builder.Append("Markup");
            return builder.ToString();
        }
    }
}
