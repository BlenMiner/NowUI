using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;
using System.IO;

public class NowUIFontCompiler : Editor
{
    const int AtlasSize = 64;
    const int PixelRange = 16;

    static string ToProjectFullPath(string assetPath)
    {
        var projectPath = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectPath, assetPath));
    }

    [MenuItem("Assets/NowUI/Compile Font")]
    public static void CompileFonts()
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

                EditorUtility.DisplayProgressBar("Compile Font", target.name, i / (float)selection.Length);

                if (!NowFontCompiler.TryCompile(
                    File.ReadAllBytes(ToProjectFullPath(fontPath)),
                    AtlasSize,
                    PixelRange,
                    out NowFont font,
                    out string error))
                {
                    Debug.LogError("Failed to compile " + target.name + "\n" + error);
                    continue;
                }

                try
                {
                    if (AssetDatabase.LoadAssetAtPath<NowFont>(newFontPath) != null)
                        AssetDatabase.DeleteAsset(newFontPath);

                    font.name = target.name;
                    font.Atlas.name = "Font Atlas Texture";
                    font.Material.name = "Font Material";

                    AssetDatabase.CreateAsset(font, newFontPath);
                    AssetDatabase.AddObjectToAsset(font.Atlas, newFontPath);
                    AssetDatabase.AddObjectToAsset(font.Material, newFontPath);
                    EditorUtility.SetDirty(font);
                    AssetDatabase.ImportAsset(newFontPath);
                    AssetDatabase.SaveAssets();
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
