using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NowUI;
using NowUI.Internal;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class NowLottieAssetTests
{
    const string MinimalJson = "{\"v\":\"5.5.7\",\"fr\":30,\"ip\":0,\"op\":60,\"w\":200,\"h\":100,\"layers\":[]}";
    const string AlternateJson = "{\"v\":\"5.5.7\",\"fr\":24,\"ip\":0,\"op\":24,\"w\":80,\"h\":40,\"layers\":[]}";

    [TearDown]
    public void TearDown()
    {
        NowLottieCache.Reset();
        NowLottieAsset.requestTimeoutSeconds = 30;
        NowLottieAsset.maxDownloadBytes = 64L * 1024L * 1024L;
        NowLottieAsset.maxArchiveBytes = 64L * 1024L * 1024L;
        NowLottieAsset.maxJsonBytes = 64L * 1024L * 1024L;
        NowLottieAsset.maxArchiveEntries = 512;
        NowLottieAsset.maxArchiveUncompressedBytes = 256L * 1024L * 1024L;
        NowLottieAsset.maxArchiveCompressionRatio = 1000f;
        NowLottieAsset.maxJsonDepth = 256;
        NowLottieAsset.maxJsonNodes = 2_000_000;
        NowLottieAsset.maxRedirects = 8;
        NowLottieAsset.allowInsecureHttp = true;
        NowLottieAsset.remoteUrlPolicy = null;
        NowLottieCache.maxEntries = 64;
        NowLottieCache.maxConcurrentDownloads = 4;
        NowLottieCache.maxCachedSourceBytes = 256L * 1024L * 1024L;
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

    [Test]
    public void RemoteLoadingDefaultsAreBoundedAndHttpCompatible()
    {
        Assert.IsTrue(NowLottieAsset.allowInsecureHttp);
        Assert.AreEqual(8, NowLottieAsset.maxRedirects);
        Assert.Greater(NowLottieAsset.maxDownloadBytes, 0L);
        Assert.Greater(NowLottieAsset.requestTimeoutSeconds, 0);
        Assert.Greater(NowLottieCache.maxEntries, 0);
        Assert.Greater(NowLottieCache.maxConcurrentDownloads, 0);
    }

    [Test]
    public void BoundedDownloadHandlerRefusesChunkBeforeRetainingPastLimit()
    {
        using var handler = new NowBoundedDownloadHandler(4);

        Assert.IsTrue(handler.ReceiveDataForTesting(new byte[] { 1, 2, 3 }, 3));
        Assert.IsFalse(handler.ReceiveDataForTesting(new byte[] { 4, 5 }, 2));
        Assert.IsTrue(handler.limitExceeded);
        Assert.AreEqual(3L, handler.receivedByteCount);
        Assert.IsNull(handler.GetBytes());
    }

    [Test]
    public void BoundedDownloadHandlerAcceptsExactLimitAndRejectsOversizedHeader()
    {
        using (var exact = new NowBoundedDownloadHandler(4))
        {
            exact.ReceiveContentLengthForTesting(4);
            Assert.IsTrue(exact.ReceiveDataForTesting(new byte[] { 1, 2, 3, 4 }, 4));
            Assert.AreEqual(4L, exact.retainedCapacityBytes);
            Assert.AreEqual(0, exact.segmentCount);
            exact.CompleteForTesting();
            Assert.IsTrue(exact.completedWithoutConsolidation);
            Assert.AreEqual(4L, exact.retainedCapacityBytes);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, exact.GetBytes());
        }

        using var declaredOversize = new NowBoundedDownloadHandler(4);
        declaredOversize.ReceiveContentLengthForTesting(5);
        Assert.IsTrue(declaredOversize.limitExceeded);
        Assert.AreEqual(0L, declaredOversize.receivedByteCount);
    }

    [Test]
    public void BoundedDownloadHandlerUsesCappedSegmentsForUnknownLength()
    {
        using var handler = new NowBoundedDownloadHandler(100_000);

        Assert.IsTrue(handler.ReceiveDataForTesting(new byte[] { 1, 2, 3 }, 3));
        Assert.IsTrue(handler.ReceiveDataForTesting(new byte[] { 4, 5, 6, 7 }, 4));
        Assert.AreEqual(1, handler.segmentCount);
        Assert.AreEqual(64L * 1024L, handler.retainedCapacityBytes);
        Assert.LessOrEqual(handler.retainedCapacityBytes, handler.byteLimit);

        handler.CompleteForTesting();

        Assert.IsFalse(handler.completedWithoutConsolidation);
        Assert.AreEqual(0, handler.segmentCount);
        Assert.AreEqual(7L, handler.retainedCapacityBytes);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, handler.GetBytes());
    }

    [Test]
    public void JsonParserEnforcesDepthAndNodeLimits()
    {
        Assert.Throws<FormatException>(() => NowJsonValue.Parse("[[[[0]]]]", 4, 100));
        Assert.Throws<FormatException>(() => NowJsonValue.Parse("[0,1,2]", 32, 3));
        Assert.DoesNotThrow(() => NowJsonValue.Parse("[0,1,2]", 32, 4));
    }

    [Test]
    public void SetSourceUsesConfiguredJsonLimits()
    {
        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            NowLottieAsset.maxJsonBytes = 16;
            Assert.Throws<FormatException>(() => asset.SetSource(MinimalJson));

            NowLottieAsset.maxJsonBytes = 1024;
            NowLottieAsset.maxJsonDepth = 3;
            string nested = "{\"layers\":[],\"extra\":[[[0]]]}";
            Assert.Throws<FormatException>(() => asset.SetSource(nested));
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void DotLottieExtractionEnforcesExpandedSizeAndCompressionRatio()
    {
        byte[] archive = CreateZip(
            System.IO.Compression.CompressionLevel.Optimal,
            ("animations/spinner.json", MinimalJson),
            ("unused/padding.bin", new string('x', 8192)));

        NowLottieAsset.maxArchiveUncompressedBytes = 128;
        Assert.Throws<FormatException>(() => NowLottieAsset.ExtractSourceJson(archive));

        NowLottieAsset.maxArchiveUncompressedBytes = 1024 * 1024;
        NowLottieAsset.maxArchiveCompressionRatio = 2f;
        Assert.Throws<FormatException>(() => NowLottieAsset.ExtractSourceJson(archive));
    }

    [Test]
    public void DotLottiePreflightRejectsClassicEntryCountBeforeZipArchive()
    {
        byte[] archive = CreateZip(
            ("animations/one.json", MinimalJson),
            ("animations/two.json", MinimalJson),
            ("animations/three.json", MinimalJson));

        var exception = Assert.Throws<FormatException>(() =>
            NowZipArchivePreflight.Validate(archive, 2));

        StringAssert.Contains("3 entries", exception.Message);
    }

    [Test]
    public void DotLottiePreflightRejectsZip64EntryCountBeforeZipArchive()
    {
        Assert.AreEqual(0UL, NowZipArchivePreflight.Validate(
            CreateZip64EndRecords(entryCount: 0),
            512));

        byte[] archive = CreateZip64EndRecords(entryCount: 513);

        var exception = Assert.Throws<FormatException>(() =>
            NowZipArchivePreflight.Validate(archive, 512));

        StringAssert.Contains("513 entries", exception.Message);
    }

    [Test]
    public void DotLottiePreflightRejectsLiedEntryCountAndTruncatedArchive()
    {
        byte[] archive = CreateZip(
            ("animations/one.json", MinimalJson),
            ("animations/two.json", MinimalJson),
            ("animations/three.json", MinimalJson));
        int endRecord = archive.Length - 22;
        WriteUInt16(archive, endRecord + 8, 1);
        WriteUInt16(archive, endRecord + 10, 1);

        var mismatch = Assert.Throws<FormatException>(() =>
            NowZipArchivePreflight.Validate(archive, 512));
        StringAssert.Contains("declared and actual", mismatch.Message);

        var truncated = Assert.Throws<FormatException>(() =>
            NowLottieAsset.ExtractSourceJson(new byte[] { (byte)'P', (byte)'K', 3, 4 }));
        StringAssert.Contains("truncated", truncated.Message);
    }

    [Test]
    public void AutomaticCacheStaysWithinEntryLimit()
    {
        var first = ScriptableObject.CreateInstance<NowLottieAsset>();
        var second = ScriptableObject.CreateInstance<NowLottieAsset>();
        var third = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            first.SetSource(MinimalJson);
            second.SetSource(MinimalJson);
            third.SetSource(MinimalJson);
            NowLottieCache.maxEntries = 2;

            NowLottieCache.SetAsset("https://example.com/first.json", first);
            NowLottieCache.SetAsset("https://example.com/second.json", second);
            NowLottieCache.SetAsset("https://example.com/third.json", third);

            Assert.AreEqual(2, NowLottieCache.cachedEntryCount);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(third);
        }
    }

    [Test]
    public void RemoteUrlPolicyRejectsBeforeARequestStarts()
    {
        NowLottieAsset.allowInsecureHttp = false;

        var state = NowLottieCache.GetState("http://example.com/spinner.json", out var asset, out var error);

        Assert.AreEqual(NowLottieCacheState.Failed, state);
        Assert.IsNull(asset);
        StringAssert.Contains("HTTP", error);
        Assert.AreEqual(0, NowLottieCache.cachedEntryCount);

        NowLottieAsset.remoteUrlPolicy = _ => false;
        state = NowLottieCache.GetState("https://example.com/spinner.json", out asset, out error);

        Assert.AreEqual(NowLottieCacheState.Failed, state);
        StringAssert.Contains("remoteUrlPolicy", error);
        Assert.AreEqual(0, NowLottieCache.cachedEntryCount);
    }

    static byte[] CreateZip(params (string path, string content)[] entries)
    {
        return CreateZip(System.IO.Compression.CompressionLevel.NoCompression, entries);
    }

    static byte[] CreateZip(
        System.IO.Compression.CompressionLevel compressionLevel,
        params (string path, string content)[] entries)
    {
        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.path, compressionLevel);

                using var writer = new StreamWriter(zipEntry.Open(), Encoding.UTF8);
                writer.Write(entry.content);
            }
        }

        return memory.ToArray();
    }

    static byte[] CreateZip64EndRecords(ulong entryCount)
    {
        const int zip64RecordLength = 56;
        const int locatorLength = 20;
        const int endRecordLength = 22;
        var bytes = new byte[zip64RecordLength + locatorLength + endRecordLength];
        int locator = zip64RecordLength;
        int endRecord = locator + locatorLength;

        WriteUInt32(bytes, 0, 0x06064b50u);
        WriteUInt64(bytes, 4, 44UL);
        WriteUInt64(bytes, 24, entryCount);
        WriteUInt64(bytes, 32, entryCount);
        WriteUInt32(bytes, locator, 0x07064b50u);
        WriteUInt64(bytes, locator + 8, 0UL);
        WriteUInt32(bytes, locator + 16, 1u);
        WriteUInt32(bytes, endRecord, 0x06054b50u);
        WriteUInt16(bytes, endRecord + 8, ushort.MaxValue);
        WriteUInt16(bytes, endRecord + 10, ushort.MaxValue);
        WriteUInt32(bytes, endRecord + 12, uint.MaxValue);
        WriteUInt32(bytes, endRecord + 16, uint.MaxValue);
        return bytes;
    }

    static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        WriteUInt16(bytes, offset, (ushort)value);
        WriteUInt16(bytes, offset + 2, (ushort)(value >> 16));
    }

    static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        WriteUInt32(bytes, offset, (uint)value);
        WriteUInt32(bytes, offset + 4, (uint)(value >> 32));
    }
}
