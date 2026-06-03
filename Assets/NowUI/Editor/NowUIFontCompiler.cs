using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;
using System.IO;

public class NowUIFontCompiler : Editor
{
    const int ATLAS_SIZE = 64;
    const int PIXEL_RANGE = 16;
    const string EXTRA_CHARACTERS =
        "\U0001F600\U0001F603\U0001F604\U0001F601\U0001F606\U0001F605\U0001F602\U0001F923" +
        "\U0001F60A\U0001F60D\U0001F618\U0001F60E\U0001F44D\U0001F44E\U0001F64F" +
        "\u2764\uFE0F\u2728\U0001F525\U0001F389";

    static string ToProjectFullPath(string assetPath)
    {
        var projectPath = Directory.GetParent(Application.dataPath)?.FullName;
        if (projectPath != null) return Path.GetFullPath(Path.Combine(projectPath, assetPath));
        return string.Empty;
    }

    [MenuItem("Assets/NowUI/Compile Font")]
    public static void CompileFonts()
    {
        CompileSelectedFonts(false);
    }

    [MenuItem("Assets/NowUI/Compile Dynamic Font")]
    public static void CompileDynamicFonts()
    {
        CompileSelectedFonts(true);
    }

    static void CompileSelectedFonts(bool dynamicFont)
    {
        var selection = Selection.objects;

        try
        {
            for (int i = 0; i < selection.Length; ++i)
            {
                var target = selection[i];

                if (!(target is Font))
                    continue;

                var fontPath = AssetDatabase.GetAssetPath(target);
                var newFontPath = $"{fontPath}.asset";
                var fontData = File.ReadAllBytes(ToProjectFullPath(fontPath));

                EditorUtility.DisplayProgressBar(dynamicFont ? "Compile Dynamic Font" : "Compile Font", target.name, i / (float)selection.Length);

                if (!NowFontCompiler.TryCompile(
                    fontData,
                    ATLAS_SIZE,
                    PIXEL_RANGE,
                    EXTRA_CHARACTERS,
                    out NowFont font,
                    out string error))
                {
                    Debug.LogError("Failed to compile " + target.name + "\n" + error);
                    continue;
                }

                try
                {
                    bool usedCodepointFallback = NowFontCompiler.usedCodepointFallback;

                    if (AssetDatabase.LoadAssetAtPath<NowFont>(newFontPath) != null)
                        AssetDatabase.DeleteAsset(newFontPath);

                    font.name = target.name;
                    font.atlas.name = "Font Atlas Texture";
                    font.material.name = "Font Material";
                    font.dynamicFont = dynamicFont;
                    font.dynamicFontBytes = dynamicFont ? fontData : null;
                    font.dynamicAtlasSize = ATLAS_SIZE;
                    font.dynamicPixelRange = PIXEL_RANGE;

                    AssetDatabase.CreateAsset(font, newFontPath);
                    AssetDatabase.AddObjectToAsset(font.atlas, newFontPath);
                    AssetDatabase.AddObjectToAsset(font.material, newFontPath);
                    EditorUtility.SetDirty(font);
                    EditorUtility.SetDirty(font.atlas);
                    EditorUtility.SetDirty(font.material);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(newFontPath, ImportAssetOptions.ForceUpdate);

                    if (usedCodepointFallback)
                        Debug.LogWarning("Compiled " + target.name + " without extra emoji glyphs because the loaded NowUI native plugin does not expose nowui_compile_font_from_memory_with_codepoints. Rebuild/import the latest NowUI native plugins for emoji codepoint compilation.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Failed to compile " + target.name + "\n" + ex.Message + "\n" + ex.StackTrace);
                }
            }
        }
        finally
        {
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }
    }
}
