using System.IO;
using System.IO.Compression;
using System.Text;
using NowUI;
using NUnit.Framework;
using UnityEngine;

public class NowLottieAssetTests
{
    const string MinimalJson = "{\"v\":\"5.5.7\",\"fr\":30,\"ip\":0,\"op\":60,\"w\":200,\"h\":100,\"layers\":[]}";
    const string AlternateJson = "{\"v\":\"5.5.7\",\"fr\":24,\"ip\":0,\"op\":24,\"w\":80,\"h\":40,\"layers\":[]}";

    [TearDown]
    public void TearDown()
    {
        NowLottieCache.Reset();
    }

    [Test]
    public void SetSourceAcceptsPlainJsonBytes()
    {
        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            asset.SetSource(Encoding.UTF8.GetBytes(MinimalJson));

            Assert.IsTrue(asset.hasJson);
            Assert.AreEqual(200f, asset.width);
            Assert.AreEqual(100f, asset.height);
            Assert.AreEqual(30f, asset.frameRate);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void SetSourceAcceptsDotLottieArchiveBytes()
    {
        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            asset.SetSource(CreateZip(
                ("manifest.json", "{\"animations\":[]}"),
                ("animations/spinner.json", MinimalJson)));

            Assert.IsTrue(asset.hasJson);
            Assert.AreEqual(200f, asset.width);
            Assert.AreEqual(100f, asset.height);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void ExtractSourceJsonPrefersAnimationEntry()
    {
        var bytes = CreateZip(
            ("manifest.json", "{\"animations\":[]}"),
            ("preview.json", AlternateJson),
            ("animations/spinner.json", MinimalJson));

        Assert.AreEqual(MinimalJson, NowLottieAsset.ExtractSourceJson(bytes));
    }

    [Test]
    public void CacheCanInjectLoadedAssetForUrl()
    {
        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            asset.SetSource(MinimalJson);
            NowLottieCache.SetAsset("https://example.com/spinner.json", asset);

            var state = NowLottieCache.GetState("https://example.com/spinner.json", out var cached, out var error);

            Assert.AreEqual(NowLottieCacheState.Loaded, state);
            Assert.AreSame(asset, cached);
            Assert.IsNull(error);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    static byte[] CreateZip(params (string path, string content)[] entries)
    {
        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.path, System.IO.Compression.CompressionLevel.NoCompression);

                using var writer = new StreamWriter(zipEntry.Open(), Encoding.UTF8);
                writer.Write(entry.content);
            }
        }

        return memory.ToArray();
    }
}
