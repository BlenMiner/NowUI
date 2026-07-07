using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NowUI.Editor
{
    /// <summary>
    /// Compiles a set of selected .ttf fonts into NowFont assets and wires them
    /// into a <see cref="NowFontFamily"/> in one step. Styles resolve from the
    /// "-Regular" / "-Bold" / "-Italic" / "-BoldItalic" name suffixes, and the
    /// family asset is named after the common prefix, e.g. selecting the four
    /// JetBrainsMono-*.ttf files produces JetBrainsMono.asset.
    /// </summary>
    public static class NowFontFamilyCompiler
    {
        const int ATLAS_SIZE = 64;
        const int PIXEL_RANGE = 16;

        [MenuItem("Assets/NowUI/Compile Font Family")]
        public static void CompileFamily()
        {
            var variants = new Dictionary<string, NowFont>();
            string folder = null;
            string familyName = null;

            try
            {
                var selection = Selection.objects;

                for (int i = 0; i < selection.Length; ++i)
                {
                    if (!(selection[i] is Font target))
                        continue;

                    string fontPath = AssetDatabase.GetAssetPath(target);
                    string assetPath = fontPath + ".asset";
                    EditorUtility.DisplayProgressBar("Compile Font Family", target.name, i / (float)selection.Length);

                    byte[] fontData = File.ReadAllBytes(Path.GetFullPath(fontPath));

                    if (!NowUI.NowFontCompiler.TryCompile(fontData, ATLAS_SIZE, PIXEL_RANGE, out NowFont font, out string error))
                    {
                        Debug.LogError($"Failed to compile {target.name}\n{error}");
                        continue;
                    }

                    if (AssetDatabase.LoadAssetAtPath<NowFont>(assetPath) != null)
                        AssetDatabase.DeleteAsset(assetPath);

                    font.name = target.name;
                    AssetDatabase.CreateAsset(font, assetPath);
                    EditorUtility.SetDirty(font);

                    string style = StyleSuffix(target.name, out string prefix);
                    variants[style] = font;
                    folder ??= Path.GetDirectoryName(fontPath)?.Replace('\\', '/');
                    familyName ??= prefix;
                }

                AssetDatabase.SaveAssets();

                if (variants.Count == 0 || folder == null)
                {
                    Debug.LogWarning("Select the .ttf font files of one family (e.g. JetBrainsMono-Regular/-Bold/-Italic/-BoldItalic).");
                    return;
                }

                string familyPath = $"{folder}/{familyName}.asset";
                var family = AssetDatabase.LoadAssetAtPath<NowFontFamily>(familyPath);

                if (family == null)
                {
                    family = ScriptableObject.CreateInstance<NowFontFamily>();
                    AssetDatabase.CreateAsset(family, familyPath);
                }

                var serialized = new SerializedObject(family);
                Assign(serialized, "_regular", variants, "Regular");
                Assign(serialized, "_bold", variants, "Bold");
                Assign(serialized, "_italic", variants, "Italic");
                Assign(serialized, "_boldItalic", variants, "BoldItalic");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(family);
                AssetDatabase.SaveAssets();

                Debug.Log($"Compiled font family '{familyName}' with {variants.Count} style(s) at {familyPath}");
                Selection.activeObject = family;
            }
            finally
            {
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            }
        }

        static void Assign(SerializedObject family, string property, Dictionary<string, NowFont> variants, string style)
        {
            if (variants.TryGetValue(style, out NowFont font))
                family.FindProperty(property).objectReferenceValue = font;
        }

        static string StyleSuffix(string fontName, out string prefix)
        {
            int dash = fontName.LastIndexOf('-');

            if (dash <= 0)
            {
                prefix = fontName;
                return "Regular";
            }

            prefix = fontName.Substring(0, dash);
            string suffix = fontName.Substring(dash + 1);

            switch (suffix.ToLowerInvariant())
            {
                case "bolditalic": return "BoldItalic";
                case "bold": return "Bold";
                case "italic": return "Italic";
                default: return "Regular";
            }
        }
    }
}
