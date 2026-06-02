using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;
using System.IO;

public class NowUIFontCompiler : Editor
{
    const int ATLAS_SIZE = 64;
    const int PIXEL_RANGE = 16;

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
                    ATLAS_SIZE,
                    PIXEL_RANGE,
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
                    font.atlas.name = "Font Atlas Texture";
                    font.material.name = "Font Material";

                    AssetDatabase.CreateAsset(font, newFontPath);
                    AssetDatabase.AddObjectToAsset(font.atlas, newFontPath);
                    AssetDatabase.AddObjectToAsset(font.material, newFontPath);
                    EditorUtility.SetDirty(font);
                    EditorUtility.SetDirty(font.atlas);
                    EditorUtility.SetDirty(font.material);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(newFontPath, ImportAssetOptions.ForceUpdate);
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
