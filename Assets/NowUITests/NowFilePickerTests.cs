using System.IO;
using NUnit.Framework;
using NowUI;
using UnityEngine;

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

    [TestCase("preview.png")]
    [TestCase("preview.JPG")]
    [TestCase("preview.JpEg")]
    public void PreviewableImagesSupportPngAndJpegCaseInsensitively(string path)
    {
        Assert.IsTrue(NowFilePickerUtility.IsPreviewableImage(path));
    }

    [TestCase("preview.gif")]
    [TestCase("preview.webp")]
    [TestCase("preview")]
    [TestCase("")]
    [TestCase(null)]
    public void PreviewableImagesRejectUnsupportedOrMissingExtensions(string path)
    {
        Assert.IsFalse(NowFilePickerUtility.IsPreviewableImage(path));
    }

    [TestCase(400, 200, 128, 128, 64)]
    [TestCase(200, 400, 128, 64, 128)]
    [TestCase(320, 320, 96, 96, 96)]
    public void ThumbnailSizeFitsWithinMaximumAndPreservesAspect(
        int width,
        int height,
        int maxDimension,
        int expectedWidth,
        int expectedHeight)
    {
        Assert.AreEqual(
            new Vector2Int(expectedWidth, expectedHeight),
            NowFilePickerUtility.ThumbnailSize(width, height, maxDimension));
    }

    [Test]
    public void ThumbnailSizeDoesNotUpscaleAndRejectsInvalidInputs()
    {
        Assert.AreEqual(new Vector2Int(32, 16), NowFilePickerUtility.ThumbnailSize(32, 16, 128));
        Assert.AreEqual(Vector2Int.zero, NowFilePickerUtility.ThumbnailSize(0, 16, 128));
        Assert.AreEqual(Vector2Int.zero, NowFilePickerUtility.ThumbnailSize(32, -1, 128));
        Assert.AreEqual(Vector2Int.zero, NowFilePickerUtility.ThumbnailSize(32, 16, 0));
    }

    [TestCase(320f, 100f, 10f, 3)]
    [TestCase(209f, 100f, 10f, 1)]
    [TestCase(210f, 100f, 10f, 2)]
    [TestCase(40f, 100f, 10f, 1)]
    public void GridColumnCountAccountsForWidthAndGaps(
        float width,
        float preferredWidth,
        float gap,
        int expected)
    {
        Assert.AreEqual(expected, NowFilePickerUtility.GridColumnCount(width, preferredWidth, gap));
    }

    [Test]
    public void GridColumnCountHandlesInvalidWidthAndNegativeGap()
    {
        Assert.AreEqual(1, NowFilePickerUtility.GridColumnCount(0f, 100f, 8f));
        Assert.AreEqual(1, NowFilePickerUtility.GridColumnCount(200f, 0f, 8f));
        Assert.AreEqual(2, NowFilePickerUtility.GridColumnCount(200f, 100f, -8f));
    }

    [Test]
    public void ClampViewClampsInvalidEnumValues()
    {
        Assert.AreEqual(NowFilePickerView.Details, NowFilePickerUtility.ClampView((NowFilePickerView)(-1)));
        Assert.AreEqual(NowFilePickerView.LargeThumbnails, NowFilePickerUtility.ClampView((NowFilePickerView)99));
    }

    [Test]
    public void FitModalRectCentersPreferredSizeInSurface()
    {
        NowRect result = NowFilePickerUtility.FitModalRect(
            new NowRect(20f, 40f, 760f, 460f),
            new NowRect(0f, 0f, 1000f, 800f),
            8f);

        Assert.That(result.x, Is.EqualTo(120f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(170f).Within(0.001f));
        Assert.That(result.width, Is.EqualTo(760f).Within(0.001f));
        Assert.That(result.height, Is.EqualTo(460f).Within(0.001f));
    }

    [Test]
    public void FitModalRectShrinksIntoNarrowSurfaceMargin()
    {
        NowRect result = NowFilePickerUtility.FitModalRect(
            new NowRect(20f, 40f, 760f, 460f),
            new NowRect(0f, 0f, 320f, 240f),
            8f);

        Assert.That(result.x, Is.EqualTo(8f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(8f).Within(0.001f));
        Assert.That(result.xMax, Is.EqualTo(312f).Within(0.001f));
        Assert.That(result.yMax, Is.EqualTo(232f).Within(0.001f));
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
