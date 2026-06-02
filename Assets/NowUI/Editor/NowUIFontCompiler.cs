using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.IO;

public class NowUIFontCompiler : Editor
{
    static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    static string BuildArguments(string fontPath, string imagePath, string jsonPath)
    {
        return "-size 64 -pxrange 16 -type mtsdf " +
            $"-font {QuoteArgument(fontPath)} " +
            $"-format png -imageout {QuoteArgument(imagePath)} " +
            $"-json {QuoteArgument(jsonPath)} -square4";
    }

    static string FormatProcessOutput(string output, string error)
    {
        string details = string.Empty;

        if (!string.IsNullOrWhiteSpace(error))
            details += "\n" + error.Trim();

        if (!string.IsNullOrWhiteSpace(output))
            details += "\n" + output.Trim();

        return details;
    }

    [MenuItem("Assets/NowUI/Compile Font")]
    public static void CompileFonts()
    {
        var msdf = Resources.Load<TextAsset>("msdf-atlas-gen");

        if (msdf == null)
        {
            Debug.LogError("Failed to load NowUI font compiler resource: msdf-atlas-gen");
            return;
        }

        string compilerPath = AssetDatabase.GetAssetPath(msdf);

        if (string.IsNullOrEmpty(compilerPath))
        {
            Debug.LogError("Failed to resolve NowUI font compiler path.");
            return;
        }

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

                string output;
                string error;

                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.FileName = compilerPath;
                        process.StartInfo.Arguments = BuildArguments(fontPath, imagePath, jsonPath);

                        process.Start();

                        var outputTask = process.StandardOutput.ReadToEndAsync();
                        var errorTask = process.StandardError.ReadToEndAsync();

                        process.WaitForExit();

                        output = outputTask.Result;
                        error = errorTask.Result;

                        if (process.ExitCode != 0)
                        {
                            Debug.LogError("Failed to compile " + target.name + FormatProcessOutput(output, error));
                            continue;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Failed to run NowUI font compiler for " + target.name + "\n" + ex.Message);
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
