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
                var imagePath = $"{fontPath}.png";
                var jsonPath = $"{fontPath}.json";

                EditorUtility.DisplayProgressBar("Compile Font", target.name, i / (float)selection.Length);

                if (!NowUIFontCompilerNative.TryCompileFont(
                    ToProjectFullPath(fontPath),
                    ToProjectFullPath(imagePath),
                    ToProjectFullPath(jsonPath),
                    AtlasSize,
                    PixelRange,
                    out string error))
                {
                    Debug.LogError("Failed to compile " + target.name + "\n" + error);
                    continue;
                }

                AssetDatabase.Refresh();

                try
                {
                    var newFontPath = $"{fontPath}.asset";

                    if (File.Exists(newFontPath)) AssetDatabase.DeleteAsset(newFontPath);
                    AssetDatabase.Refresh();

                    NowFont font = CreateInstance(typeof(NowFont)) as NowFont;
                    AssetDatabase.CreateAsset(font, newFontPath);

                    var json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);

                    if (json == null)
                        throw new IOException("Failed to load generated atlas JSON: " + jsonPath);

                    Texture2D texture = new Texture2D(1, 1);
                    texture.name = "Font Atlas Texture";

                    if (!texture.LoadImage(File.ReadAllBytes(imagePath), true))
                        throw new IOException("Failed to load generated atlas texture: " + imagePath);

                    var materialTemplate = Resources.Load<Material>("NowUI/TxtMaterial");

                    if (materialTemplate == null)
                        throw new IOException("Failed to load NowUI text material template.");

                    Material fontMat = Instantiate(materialTemplate);

                    fontMat.mainTexture = texture;

                    AssetDatabase.AddObjectToAsset(texture, newFontPath);
                    AssetDatabase.AddObjectToAsset(fontMat, newFontPath);
                    AssetDatabase.Refresh();

                    font.Atlas = texture;
                    font.Material = fontMat;
                    font.AtlasInfo = JsonUtility.FromJson<NowFontAtlasInfo>(json.text);

                    EditorUtility.SetDirty(font);

                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(texture));

                    AssetDatabase.DeleteAsset(imagePath);
                    AssetDatabase.DeleteAsset(jsonPath);
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
