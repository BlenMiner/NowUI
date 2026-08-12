using System.IO;
using NUnit.Framework;
using NowUI;

public class NowFilePickerTests
{
    [Test]
    public void FileFilterMatchesExtensionsCaseInsensitively()
    {
        var filters = NowFilePickerUtility.NormalizeFilters(new[]
        {
            new NowFileFilter("Images", ".png", "JPG")
        });

        Assert.IsTrue(NowFilePickerUtility.MatchesFilter("icon.PNG", filters[0]));
        Assert.IsTrue(NowFilePickerUtility.MatchesFilter("photo.jpg", filters[0]));
        Assert.IsFalse(NowFilePickerUtility.MatchesFilter("notes.txt", filters[0]));
    }

    [Test]
    public void WildcardFilterMatchesAnyFile()
    {
        var filters = NowFilePickerUtility.NormalizeFilters(new[]
        {
            new NowFileFilter("All", "*.*")
        });

        Assert.IsTrue(NowFilePickerUtility.MatchesFilter("archive.zip", filters[0]));
        Assert.IsTrue(NowFilePickerUtility.MatchesFilter("readme", filters[0]));
    }

    [Test]
    public void BuildSavePathAddsFilterExtensionWhenMissing()
    {
        string directory = Path.GetTempPath();
        var filters = NowFilePickerUtility.NormalizeFilters(new[]
        {
            new NowFileFilter("Json", "json")
        });

        string path = NowFilePickerUtility.BuildSavePath(directory, "settings", filters, 0, null, out string error);

        Assert.IsNull(error);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(directory, "settings.json")), path);
    }

    [Test]
    public void BuildSavePathKeepsExistingExtension()
    {
        string directory = Path.GetTempPath();
        var filters = NowFilePickerUtility.NormalizeFilters(new[]
        {
            new NowFileFilter("Json", "json")
        });

        string path = NowFilePickerUtility.BuildSavePath(directory, "settings.txt", filters, 0, "json", out string error);

        Assert.IsNull(error);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(directory, "settings.txt")), path);
    }

    [Test]
    public void BuildSavePathRejectsInvalidFileName()
    {
        string path = NowFilePickerUtility.BuildSavePath(
            Path.GetTempPath(),
            "bad\0name.json",
            new NowFileFilter[0],
            0,
            null,
            out string error);

        Assert.IsNull(path);
        Assert.AreEqual("Invalid file name", error);
    }

    [Test]
    public void BuildOpenPathRequiresExistingFilteredFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NowFilePickerTests");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "scene.json");
        File.WriteAllText(file, "{}");

        try
        {
            var filters = NowFilePickerUtility.NormalizeFilters(new[]
            {
                new NowFileFilter("Json", "json")
            });

            string path = NowFilePickerUtility.BuildOpenPath(directory, "scene.json", filters, 0, out string error);

            Assert.IsNull(error);
            Assert.AreEqual(Path.GetFullPath(file), path);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);

            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }
    }

    [Test]
    public void BuildOpenPathRejectsInvalidFileName()
    {
        string path = NowFilePickerUtility.BuildOpenPath(
            Path.GetTempPath(),
            "bad\0name.json",
            new NowFileFilter[0],
            0,
            out string error);

        Assert.IsNull(path);
        Assert.AreEqual("Invalid file name", error);
    }
}
