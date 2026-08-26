using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NowUI;

public class NowFilePickerUserFoldersTests
{
    [Test]
    public void ResolveCandidatesPreservesOrderCanonicalizesAndKeepsFirstDuplicate()
    {
        var candidates = new List<NowFilePickerUserFolder>
        {
            new NowFilePickerUserFolder(1, "Desktop", "desktop", "D"),
            new NowFilePickerUserFolder(2, "Downloads", "downloads", "W"),
            new NowFilePickerUserFolder(3, "Desktop alias", "DESKTOP", "A")
        };
        var result = new List<NowFilePickerUserFolder>();

        NowFilePickerUserFolders.ResolveCandidates(
            candidates,
            path => "/canonical/" + path,
            _ => true,
            StringComparer.OrdinalIgnoreCase,
            result);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Desktop", result[0].label);
        Assert.AreEqual("/canonical/desktop", result[0].path);
        Assert.AreEqual("Downloads", result[1].label);
    }

    [Test]
    public void ResolveCandidatesSkipsBlankInvalidAndMissingDirectories()
    {
        var candidates = new List<NowFilePickerUserFolder>
        {
            new NowFilePickerUserFolder(1, "Desktop", "", "D"),
            new NowFilePickerUserFolder(2, "Downloads", "invalid", "W"),
            new NowFilePickerUserFolder(3, "Documents", "missing", "F"),
            new NowFilePickerUserFolder(4, "Pictures", "present", "P")
        };
        var result = new List<NowFilePickerUserFolder>();

        NowFilePickerUserFolders.ResolveCandidates(
            candidates,
            path => path == "invalid" ? null : "/canonical/" + path,
            path => path.EndsWith("present", StringComparison.Ordinal),
            StringComparer.Ordinal,
            result);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Pictures", result[0].label);
        Assert.AreEqual("/canonical/present", result[0].path);
    }

    [Test]
    public void PathComparerIsCaseInsensitiveOnlyOnWindows()
    {
        Assert.AreEqual(0, NowFilePickerUserFolders.PathComparer(NowFilePickerUserFolderPlatform.Windows).Compare("Path", "path"));
        Assert.AreNotEqual(0, NowFilePickerUserFolders.PathComparer(NowFilePickerUserFolderPlatform.MacOS).Compare("Path", "path"));
        Assert.AreNotEqual(0, NowFilePickerUserFolders.PathComparer(NowFilePickerUserFolderPlatform.Linux).Compare("Path", "path"));
    }

    [Test]
    public void CanonicalPathTrimsTrailingSeparatorsWithoutTrimmingRoot()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NowFilePickerUserFolders");
        string withSeparator = directory + Path.DirectorySeparatorChar;
        string root = Path.GetPathRoot(Path.GetFullPath(directory));

        Assert.AreEqual(Path.GetFullPath(directory), NowFilePickerUserFolders.CanonicalPath(withSeparator));
        Assert.AreEqual(root, NowFilePickerUserFolders.CanonicalPath(root));
    }

    [Test]
    public void WindowsCandidatesUseSpecialFoldersAndKnownDownloadsInStableOrder()
    {
        string home = Path.Combine(Path.GetTempPath(), "user");
        var special = new Dictionary<Environment.SpecialFolder, string>
        {
            { Environment.SpecialFolder.DesktopDirectory, "desktop-known" },
            { Environment.SpecialFolder.MyDocuments, "documents-known" },
            { Environment.SpecialFolder.MyPictures, "pictures-known" },
            { Environment.SpecialFolder.MyMusic, "music-known" },
            { Environment.SpecialFolder.MyVideos, "videos-known" }
        };
        var candidates = new List<NowFilePickerUserFolder>();

        NowFilePickerUserFolders.BuildCandidates(
            NowFilePickerUserFolderPlatform.Windows,
            home,
            null,
            folder => special[folder],
            "downloads-known",
            candidates);

        CollectionAssert.AreEqual(
            new[] { "Desktop", "Downloads", "Documents", "Pictures", "Music", "Videos" },
            Labels(candidates));
        CollectionAssert.AreEqual(
            new[] { "desktop-known", "downloads-known", "documents-known", "pictures-known", "music-known", "videos-known" },
            Paths(candidates));
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, StableIds(candidates));
    }

    [Test]
    public void MacCandidatesUseMoviesInStableOrder()
    {
        string home = Path.Combine(Path.GetTempPath(), "mac-user");
        var candidates = new List<NowFilePickerUserFolder>();

        NowFilePickerUserFolders.BuildCandidates(
            NowFilePickerUserFolderPlatform.MacOS,
            home,
            null,
            _ => null,
            null,
            candidates);

        CollectionAssert.AreEqual(
            new[] { "Desktop", "Downloads", "Documents", "Pictures", "Music", "Movies" },
            Labels(candidates));
        Assert.AreEqual(Path.Combine(home, "Movies"), candidates[5].path);
        Assert.AreEqual(NowFilePickerUserFolders.VideosId, candidates[5].stableId);
    }

    [Test]
    public void LinuxCandidatesFollowLocalizedXdgDirectoriesAndOmitDisabledCategory()
    {
        string home = Path.Combine(Path.GetTempPath(), "linux-user");
        string config =
            "XDG_DESKTOP_DIR=\"$HOME/Bureau\"\n" +
            "XDG_DOWNLOAD_DIR=\"$HOME/Téléchargements\"\n" +
            "XDG_DOCUMENTS_DIR=\"$HOME\"\n" +
            "XDG_PICTURES_DIR=\"$HOME/Images personnalisées\"\n" +
            "XDG_MUSIC_DIR=\"$HOME/Musique\"\n" +
            "XDG_VIDEOS_DIR=\"$HOME/Vidéos\"\n";
        var candidates = new List<NowFilePickerUserFolder>();

        NowFilePickerUserFolders.BuildCandidates(
            NowFilePickerUserFolderPlatform.Linux,
            home,
            config,
            _ => null,
            null,
            candidates);

        CollectionAssert.AreEqual(
            new[] { "Desktop", "Downloads", "Pictures", "Music", "Videos" },
            Labels(candidates));
        CollectionAssert.AreEqual(
            new[]
            {
                Path.Combine(home, "Bureau"),
                Path.Combine(home, "Téléchargements"),
                Path.Combine(home, "Images personnalisées"),
                Path.Combine(home, "Musique"),
                Path.Combine(home, "Vidéos")
            },
            Paths(candidates));
    }

    [Test]
    public void LinuxCandidatesUseConventionalFallbacksWhenXdgKeyIsMissing()
    {
        string home = Path.Combine(Path.GetTempPath(), "linux-user");
        var candidates = new List<NowFilePickerUserFolder>();

        NowFilePickerUserFolders.BuildCandidates(
            NowFilePickerUserFolderPlatform.Linux,
            home,
            "# no assignments\n",
            _ => null,
            null,
            candidates);

        CollectionAssert.AreEqual(
            new[] { "Desktop", "Downloads", "Documents", "Pictures", "Music", "Videos" },
            Labels(candidates));
        Assert.AreEqual(Path.Combine(home, "Downloads"), candidates[1].path);
    }

    [Test]
    public void XdgConfigHomeMustBeAbsoluteAndOtherwiseFallsBackUnderHome()
    {
        string home = Path.Combine(Path.GetTempPath(), "linux-user");
        string configHome = Path.Combine(Path.GetTempPath(), "xdg-config");

        Assert.AreEqual(
            Path.Combine(configHome, "user-dirs.dirs"),
            NowFilePickerUserFolders.XdgUserDirsPath(home, configHome));
        Assert.AreEqual(
            Path.Combine(home, ".config", "user-dirs.dirs"),
            NowFilePickerUserFolders.XdgUserDirsPath(home, "relative-config"));
    }

    static string[] Labels(List<NowFilePickerUserFolder> folders)
    {
        var result = new string[folders.Count];

        for (int i = 0; i < folders.Count; ++i)
            result[i] = folders[i].label;

        return result;
    }

    static string[] Paths(List<NowFilePickerUserFolder> folders)
    {
        var result = new string[folders.Count];

        for (int i = 0; i < folders.Count; ++i)
            result[i] = folders[i].path;

        return result;
    }

    static int[] StableIds(List<NowFilePickerUserFolder> folders)
    {
        var result = new int[folders.Count];

        for (int i = 0; i < folders.Count; ++i)
            result[i] = folders[i].stableId;

        return result;
    }
}
