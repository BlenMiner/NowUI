using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// Imports ".lottie" files as <see cref="NowLottieAsset"/>. Accepts both plain
    /// Lottie JSON (e.g. a downloaded "lottie.json" renamed to "name.lottie") and real
    /// dotLottie archives (ZIP containers with an animations/ folder).
    /// </summary>
    [ScriptedImporter(1, "lottie")]
    public sealed class NowLottieImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytes;

            try
            {
                bytes = File.ReadAllBytes(ctx.assetPath);
            }
            catch (Exception exception)
            {
                ctx.LogImportError($"Failed to read Lottie file: {exception.Message}");
                return;
            }

            var asset = ScriptableObject.CreateInstance<NowLottieAsset>();
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            try
            {
                asset.SetSource(bytes);
            }
            catch (Exception exception)
            {
                ctx.LogImportError($"Failed to parse Lottie animation: {exception.Message}");
                DestroyImmediate(asset);
                return;
            }

            ctx.AddObjectToAsset("animation", asset);
            ctx.SetMainObject(asset);
        }
    }
}
