using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// Rebuilds the shipped theme assets from the code defaults, so
    /// <c>NowThemeColorSet.DefaultLight</c>/<c>DefaultDark</c> stay the single
    /// source of truth for the out-of-box look.
    /// </summary>
    public static class NowThemeBaker
    {
        const string ThemesFolder = "Assets/NowUI/Assets/Themes";

        [MenuItem("Tools/NowUI/Regenerate Default Themes")]
        public static void RegenerateDefaultThemes()
        {
            var light = LoadOrCreate($"{ThemesFolder}/Default.asset");
            var dark = LoadOrCreate($"{ThemesFolder}/DefaultDark.asset");

            light.ResetToDefaults(dark: false);
            dark.ResetToDefaults(dark: true);
            ClearControlRenderer(light);
            ClearControlRenderer(dark);
            light.SetCounterpart(dark);
            dark.SetCounterpart(light);
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(dark);

            var material = AssetDatabase.LoadAssetAtPath<NowThemeAsset>($"{ThemesFolder}/Material.asset");
            var materialDark = AssetDatabase.LoadAssetAtPath<NowThemeAsset>($"{ThemesFolder}/MaterialDark.asset");

            if (material != null && materialDark != null)
            {
                material.MigrateDerivedRoles();
                materialDark.MigrateDerivedRoles();
                material.SetCounterpart(materialDark);
                materialDark.SetCounterpart(material);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(materialDark);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("NowUI default themes regenerated.");
        }

        static NowThemeAsset LoadOrCreate(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<NowThemeAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<NowThemeAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        static void ClearControlRenderer(NowThemeAsset asset)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_controlRenderer").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
