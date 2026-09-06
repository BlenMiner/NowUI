using System;
using System.IO;
using NowUI.Editor;
using NUnit.Framework;

public class NowUIAgentSkillInstallerTests
{
    string _packageCache;

    [SetUp]
    public void SetUp()
    {
        _packageCache = Path.Combine(Path.GetTempPath(), "NowUIAgentSkillInstallerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_packageCache);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_packageCache))
            Directory.Delete(_packageCache, true);
    }

    [Test]
    public void CacheFallbackReturnsOnlyValidNowUIPackage()
    {
        string expected = CreateCachedPackage("a1b2c3", "com.blenminer.nowui", "1.2.3");
        CreateCachedPackage("zzz-unrelated", "com.example.other", "9.9.9");
        Directory.CreateDirectory(Path.Combine(_packageCache, "com.blenminer.nowui@missing-manifest"));

        string root = NowUIAgentSkillInstaller.ResolveUnambiguousCachedPackageRoot(_packageCache, out string version);

        Assert.AreEqual(Path.GetFullPath(expected), root);
        Assert.AreEqual("1.2.3", version);
    }

    [Test]
    public void CacheFallbackRejectsMultipleValidRevisions()
    {
        CreateCachedPackage("a1b2c3", "com.blenminer.nowui", "2.0.0");
        CreateCachedPackage("z9y8x7", "com.blenminer.nowui", "1.0.0");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            NowUIAgentSkillInstaller.ResolveUnambiguousCachedPackageRoot(_packageCache, out _));

        StringAssert.Contains("Multiple cached NowUI packages", exception.Message);
    }

    [Test]
    public void CacheFallbackReturnsNullWhenNoValidManifestExists()
    {
        CreateCachedPackage("unrelated", "com.example.other", "1.0.0");
        string malformed = Path.Combine(_packageCache, "com.blenminer.nowui@malformed");
        Directory.CreateDirectory(malformed);
        File.WriteAllText(Path.Combine(malformed, "package.json"), "not json");

        string root = NowUIAgentSkillInstaller.ResolveUnambiguousCachedPackageRoot(_packageCache, out string version);

        Assert.IsNull(root);
        Assert.IsEmpty(version);
    }

    [Test]
    public void CacheFallbackReturnsNullWhenCacheDoesNotExist()
    {
        string root = NowUIAgentSkillInstaller.ResolveUnambiguousCachedPackageRoot(
            Path.Combine(_packageCache, "missing"), out string version);

        Assert.IsNull(root);
        Assert.IsEmpty(version);
    }

    string CreateCachedPackage(string suffix, string name, string version)
    {
        string root = Path.Combine(_packageCache, "com.blenminer.nowui@" + suffix);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "package.json"),
            "{\"name\":\"" + name + "\",\"version\":\"" + version + "\"}");
        return root;
    }
}
