using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

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
        string json;

        try
        {
            json = ExtractJson(File.ReadAllBytes(ctx.assetPath));
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
            asset.SetSource(json);
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

    static string ExtractJson(byte[] bytes)
    {
        bool isZip = bytes.Length > 2 && bytes[0] == 'P' && bytes[1] == 'K';

        if (!isZip)
        {
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

        ZipArchiveEntry best = null;

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            bool isAnimation = entry.FullName.StartsWith("animations/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.StartsWith("a/", StringComparison.OrdinalIgnoreCase);

            if (isAnimation)
            {
                best = entry;
                break;
            }

            if (best == null && !entry.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                best = entry;
        }

        if (best == null)
            throw new FormatException("dotLottie archive contains no animation JSON.");

        using var entryReader = new StreamReader(best.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return entryReader.ReadToEnd();
    }
}
