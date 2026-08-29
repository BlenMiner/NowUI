using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NowUI
{
    internal readonly struct NowFilePickerUserFolder
    {
        public readonly int stableId;
        public readonly string label;
        public readonly string path;
        public readonly string icon;

        public NowFilePickerUserFolder(int stableId, string label, string path, string icon)
        {
            this.stableId = stableId;
            this.label = label;
            this.path = path;
            this.icon = icon;
        }
    }

    internal enum NowFilePickerUserFolderPlatform : byte
    {
        Other,
        Windows,
        MacOS,
        Linux
    }

    internal static class NowFilePickerUserFolders
    {
        internal const int DesktopId = 1;
        internal const int DownloadsId = 2;
        internal const int DocumentsId = 3;
        internal const int PicturesId = 4;
        internal const int MusicId = 5;
        internal const int VideosId = 6;

        const string DesktopIcon = "▣";
        const string DownloadsIcon = "↓";
        const string DocumentsIcon = "▤";
        const string PicturesIcon = "▧";
        const string MusicIcon = "♫";
        const string VideosIcon = "▶";
        const string XdgDesktop = "XDG_DESKTOP_DIR";
        const string XdgDownloads = "XDG_DOWNLOAD_DIR";
        const string XdgDocuments = "XDG_DOCUMENTS_DIR";
        const string XdgPictures = "XDG_PICTURES_DIR";
        const string XdgMusic = "XDG_MUSIC_DIR";
        const string XdgVideos = "XDG_VIDEOS_DIR";

        enum XdgDirectoryState : byte
        {
            Missing,
            Resolved,
            Disabled,
            Invalid
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static readonly Guid s_downloadsFolderId = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid folderId,
            uint flags,
            IntPtr token,
            out IntPtr path);
#endif

        /// <summary>Resolves the current platform's existing common user folders in a stable display order.</summary>
        internal static void Resolve(List<NowFilePickerUserFolder> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            NowFilePickerUserFolderPlatform platform = Platform(Application.platform);
            string home = HomeDirectory();
            string xdgText = null;

            if (platform == NowFilePickerUserFolderPlatform.Linux)
                xdgText = ReadXdgUserDirs(home, Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"));

            string downloads = platform == NowFilePickerUserFolderPlatform.Windows
                ? WindowsDownloadsPath(home)
                : null;
            var candidates = new List<NowFilePickerUserFolder>(6);
            BuildCandidates(platform, home, xdgText, SpecialFolderPath, downloads, candidates);
            ResolveCandidates(
                candidates,
                CanonicalPath,
                Directory.Exists,
                PathComparer(platform),
                output);
        }

        internal static NowFilePickerUserFolderPlatform Platform(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return NowFilePickerUserFolderPlatform.Windows;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return NowFilePickerUserFolderPlatform.MacOS;
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return NowFilePickerUserFolderPlatform.Linux;
                default:
                    return NowFilePickerUserFolderPlatform.Other;
            }
        }

        internal static StringComparer PathComparer(NowFilePickerUserFolderPlatform platform)
        {
            return platform == NowFilePickerUserFolderPlatform.Windows
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        internal static bool PathsEqual(
            string left,
            string right,
            NowFilePickerUserFolderPlatform platform)
        {
            string canonicalLeft = CanonicalPath(left);
            string canonicalRight = CanonicalPath(right);
            return !string.IsNullOrEmpty(canonicalLeft) &&
                !string.IsNullOrEmpty(canonicalRight) &&
                PathComparer(platform).Equals(canonicalLeft, canonicalRight);
        }

        internal static int IndexOfPath(
            IReadOnlyList<NowFilePickerUserFolder> folders,
            string path,
            NowFilePickerUserFolderPlatform platform)
        {
            if (folders == null || folders.Count == 0)
                return -1;

            string canonical = CanonicalPath(path);

            if (string.IsNullOrEmpty(canonical))
                return -1;

            StringComparer comparer = PathComparer(platform);

            for (int i = 0; i < folders.Count; ++i)
            {
                string candidate = CanonicalPath(folders[i].path);

                if (!string.IsNullOrEmpty(candidate) && comparer.Equals(candidate, canonical))
                    return i;
            }

            return -1;
        }

        internal static void BuildCandidates(
            NowFilePickerUserFolderPlatform platform,
            string home,
            string xdgUserDirsText,
            Func<Environment.SpecialFolder, string> specialFolderPath,
            string windowsDownloadsPath,
            List<NowFilePickerUserFolder> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            switch (platform)
            {
                case NowFilePickerUserFolderPlatform.Windows:
                    Add(output, DesktopId, "Desktop", PreferSpecial(specialFolderPath, Environment.SpecialFolder.DesktopDirectory, home, "Desktop"), DesktopIcon);
                    Add(output, DownloadsId, "Downloads", FirstNonBlank(windowsDownloadsPath, HomeChild(home, "Downloads")), DownloadsIcon);
                    Add(output, DocumentsId, "Documents", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyDocuments, home, "Documents"), DocumentsIcon);
                    Add(output, PicturesId, "Pictures", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyPictures, home, "Pictures"), PicturesIcon);
                    Add(output, MusicId, "Music", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyMusic, home, "Music"), MusicIcon);
                    Add(output, VideosId, "Videos", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyVideos, home, "Videos"), VideosIcon);
                    break;

                case NowFilePickerUserFolderPlatform.MacOS:
                    Add(output, DesktopId, "Desktop", PreferHome(home, "Desktop", specialFolderPath, Environment.SpecialFolder.DesktopDirectory), DesktopIcon);
                    Add(output, DownloadsId, "Downloads", HomeChild(home, "Downloads"), DownloadsIcon);
                    Add(output, DocumentsId, "Documents", PreferHome(home, "Documents", specialFolderPath, Environment.SpecialFolder.MyDocuments), DocumentsIcon);
                    Add(output, PicturesId, "Pictures", PreferHome(home, "Pictures", specialFolderPath, Environment.SpecialFolder.MyPictures), PicturesIcon);
                    Add(output, MusicId, "Music", PreferHome(home, "Music", specialFolderPath, Environment.SpecialFolder.MyMusic), MusicIcon);
                    Add(output, VideosId, "Movies", PreferHome(home, "Movies", specialFolderPath, Environment.SpecialFolder.MyVideos), VideosIcon);
                    break;

                case NowFilePickerUserFolderPlatform.Linux:
                    AddLinux(output, DesktopId, "Desktop", DesktopIcon, XdgDesktop, "Desktop", Environment.SpecialFolder.DesktopDirectory, home, xdgUserDirsText, specialFolderPath);
                    AddLinux(output, DownloadsId, "Downloads", DownloadsIcon, XdgDownloads, "Downloads", null, home, xdgUserDirsText, specialFolderPath);
                    AddLinux(output, DocumentsId, "Documents", DocumentsIcon, XdgDocuments, "Documents", Environment.SpecialFolder.MyDocuments, home, xdgUserDirsText, specialFolderPath);
                    AddLinux(output, PicturesId, "Pictures", PicturesIcon, XdgPictures, "Pictures", Environment.SpecialFolder.MyPictures, home, xdgUserDirsText, specialFolderPath);
                    AddLinux(output, MusicId, "Music", MusicIcon, XdgMusic, "Music", Environment.SpecialFolder.MyMusic, home, xdgUserDirsText, specialFolderPath);
                    AddLinux(output, VideosId, "Videos", VideosIcon, XdgVideos, "Videos", Environment.SpecialFolder.MyVideos, home, xdgUserDirsText, specialFolderPath);
                    break;

                default:
                    Add(output, DesktopId, "Desktop", PreferSpecial(specialFolderPath, Environment.SpecialFolder.DesktopDirectory, home, "Desktop"), DesktopIcon);
                    Add(output, DownloadsId, "Downloads", HomeChild(home, "Downloads"), DownloadsIcon);
                    Add(output, DocumentsId, "Documents", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyDocuments, home, "Documents"), DocumentsIcon);
                    Add(output, PicturesId, "Pictures", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyPictures, home, "Pictures"), PicturesIcon);
                    Add(output, MusicId, "Music", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyMusic, home, "Music"), MusicIcon);
                    Add(output, VideosId, "Videos", PreferSpecial(specialFolderPath, Environment.SpecialFolder.MyVideos, home, "Videos"), VideosIcon);
                    break;
            }
        }

        internal static void ResolveCandidates(
            IReadOnlyList<NowFilePickerUserFolder> candidates,
            Func<string, string> canonicalize,
            Func<string, bool> directoryExists,
            StringComparer comparer,
            List<NowFilePickerUserFolder> output)
        {
            if (canonicalize == null)
                throw new ArgumentNullException(nameof(canonicalize));

            if (directoryExists == null)
                throw new ArgumentNullException(nameof(directoryExists));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            if (candidates == null || candidates.Count == 0)
                return;

            var seen = new HashSet<string>(comparer ?? StringComparer.Ordinal);

            for (int i = 0; i < candidates.Count; ++i)
            {
                NowFilePickerUserFolder candidate = candidates[i];

                if (string.IsNullOrWhiteSpace(candidate.label) || string.IsNullOrWhiteSpace(candidate.path))
                    continue;

                string canonical = canonicalize(candidate.path);

                if (string.IsNullOrWhiteSpace(canonical) || !directoryExists(canonical) || !seen.Add(canonical))
                    continue;

                output.Add(new NowFilePickerUserFolder(
                    candidate.stableId,
                    candidate.label,
                    canonical,
                    candidate.icon));
            }
        }

        internal static string XdgUserDirsPath(string home, string xdgConfigHome)
        {
            string configDirectory = null;

            if (!string.IsNullOrWhiteSpace(xdgConfigHome) && Path.IsPathRooted(xdgConfigHome))
                configDirectory = xdgConfigHome;
            else if (!string.IsNullOrWhiteSpace(home))
                configDirectory = HomeChild(home, ".config");

            if (string.IsNullOrWhiteSpace(configDirectory))
                return null;

            try
            {
                return Path.Combine(configDirectory, "user-dirs.dirs");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        internal static string CanonicalPath(string path)
        {
            string full = NowFilePickerUtility.TryGetFullPath(path);

            if (string.IsNullOrEmpty(full))
                return null;

            string root;

            try
            {
                root = Path.GetPathRoot(full);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }

            int minimumLength = string.IsNullOrEmpty(root) ? 0 : root.Length;
            int length = full.Length;

            while (length > minimumLength && IsDirectorySeparator(full[length - 1]))
                --length;

            return length == full.Length ? full : full.Substring(0, length);
        }

        static void AddLinux(
            List<NowFilePickerUserFolder> output,
            int stableId,
            string label,
            string icon,
            string variable,
            string fallbackDirectory,
            Environment.SpecialFolder? specialFolder,
            string home,
            string xdgUserDirsText,
            Func<Environment.SpecialFolder, string> specialFolderPath)
        {
            XdgDirectoryState state = ResolveXdgDirectory(xdgUserDirsText, variable, home, out string path);

            if (state == XdgDirectoryState.Disabled || state == XdgDirectoryState.Invalid)
                return;

            if (state == XdgDirectoryState.Missing)
            {
                path = HomeChild(home, fallbackDirectory);

                if (string.IsNullOrWhiteSpace(path) && specialFolder.HasValue)
                    path = SafeSpecialFolder(specialFolderPath, specialFolder.Value);
            }

            Add(output, stableId, label, path, icon);
        }

        static XdgDirectoryState ResolveXdgDirectory(string text, string variable, string home, out string path)
        {
            path = null;

            if (string.IsNullOrEmpty(text))
                return XdgDirectoryState.Missing;

            using (var reader = new StringReader(text))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.TrimStart(' ', '\t', '\uFEFF');

                    if (trimmed.Length == 0 || trimmed[0] == '#')
                        continue;

                    int equals = trimmed.IndexOf('=');

                    if (equals <= 0 || !string.Equals(trimmed.Substring(0, equals).Trim(), variable, StringComparison.Ordinal))
                        continue;

                    if (!TryReadXdgValue(trimmed.Substring(equals + 1), out string rawValue))
                        return XdgDirectoryState.Invalid;

                    return ExpandXdgValue(rawValue, home, out path);
                }
            }

            return XdgDirectoryState.Missing;
        }

        static bool TryReadXdgValue(string assignmentValue, out string value)
        {
            value = null;
            string raw = assignmentValue.Trim();

            if (raw.Length == 0)
                return false;

            if (raw[0] != '"')
            {
                int comment = FindUnescapedComment(raw);
                value = (comment >= 0 ? raw.Substring(0, comment) : raw).Trim();
                return value.Length > 0;
            }

            var builder = new StringBuilder(raw.Length);
            bool escaped = false;

            for (int i = 1; i < raw.Length; ++i)
            {
                char character = raw[i];

                if (escaped)
                {
                    builder.Append('\\');
                    builder.Append(character);
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character != '"')
                {
                    builder.Append(character);
                    continue;
                }

                string remainder = raw.Substring(i + 1).Trim();

                if (remainder.Length > 0 && remainder[0] != '#')
                    return false;

                value = builder.ToString();
                return true;
            }

            return false;
        }

        static int FindUnescapedComment(string value)
        {
            bool escaped = false;

            for (int i = 0; i < value.Length; ++i)
            {
                char character = value[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '#')
                    return i;
            }

            return -1;
        }

        static XdgDirectoryState ExpandXdgValue(string rawValue, string home, out string path)
        {
            path = null;

            if (TryHomePrefix(rawValue, "$HOME", out string suffix) ||
                TryHomePrefix(rawValue, "${HOME}", out suffix) ||
                TryHomePrefix(rawValue, "~", out suffix))
            {
                if (string.IsNullOrWhiteSpace(home))
                    return XdgDirectoryState.Invalid;

                string relative = DecodeXdgEscapes(suffix).TrimStart('/', '\\');

                if (relative.Length == 0)
                    return XdgDirectoryState.Disabled;

                path = HomeChild(home, relative.Replace('/', Path.DirectorySeparatorChar));
                return string.IsNullOrWhiteSpace(path) ? XdgDirectoryState.Invalid : XdgDirectoryState.Resolved;
            }

            string decoded = DecodeXdgEscapes(rawValue);

            if (!Path.IsPathRooted(decoded))
                return XdgDirectoryState.Invalid;

            path = decoded;
            return XdgDirectoryState.Resolved;
        }

        static bool TryHomePrefix(string value, string prefix, out string suffix)
        {
            suffix = null;

            if (!value.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            if (value.Length > prefix.Length && value[prefix.Length] != '/' && value[prefix.Length] != '\\')
                return false;

            suffix = value.Substring(prefix.Length);
            return true;
        }

        static string DecodeXdgEscapes(string value)
        {
            int firstEscape = value.IndexOf('\\');

            if (firstEscape < 0)
                return value;

            var builder = new StringBuilder(value.Length);
            builder.Append(value, 0, firstEscape);

            for (int i = firstEscape; i < value.Length; ++i)
            {
                char character = value[i];

                if (character == '\\' && i + 1 < value.Length)
                    character = value[++i];

                builder.Append(character);
            }

            return builder.ToString();
        }

        static bool IsDirectorySeparator(char character)
        {
            return character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
        }

        static void Add(List<NowFilePickerUserFolder> output, int stableId, string label, string path, string icon)
        {
            if (!string.IsNullOrWhiteSpace(path))
                output.Add(new NowFilePickerUserFolder(stableId, label, path, icon));
        }

        static string PreferSpecial(
            Func<Environment.SpecialFolder, string> specialFolderPath,
            Environment.SpecialFolder specialFolder,
            string home,
            string fallbackDirectory)
        {
            return FirstNonBlank(
                SafeSpecialFolder(specialFolderPath, specialFolder),
                HomeChild(home, fallbackDirectory));
        }

        static string PreferHome(
            string home,
            string directory,
            Func<Environment.SpecialFolder, string> specialFolderPath,
            Environment.SpecialFolder specialFolder)
        {
            return FirstNonBlank(
                HomeChild(home, directory),
                SafeSpecialFolder(specialFolderPath, specialFolder));
        }

        static string SafeSpecialFolder(
            Func<Environment.SpecialFolder, string> specialFolderPath,
            Environment.SpecialFolder specialFolder)
        {
            if (specialFolderPath == null)
                return null;

            try
            {
                return specialFolderPath(specialFolder);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        static string FirstNonBlank(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
        }

        static string HomeChild(string home, string child)
        {
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(child))
                return null;

            try
            {
                return Path.Combine(home, child);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        static string HomeDirectory()
        {
            string home = SpecialFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrWhiteSpace(home))
                return home;

            home = Environment.GetEnvironmentVariable("HOME");

            if (!string.IsNullOrWhiteSpace(home))
                return home;

            return Environment.GetEnvironmentVariable("USERPROFILE");
        }

        static string SpecialFolderPath(Environment.SpecialFolder specialFolder)
        {
            try
            {
                return Environment.GetFolderPath(specialFolder);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        static string ReadXdgUserDirs(string home, string xdgConfigHome)
        {
            string path = XdgUserDirsPath(home, xdgConfigHome);

            if (string.IsNullOrEmpty(path))
                return null;

            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        static string WindowsDownloadsPath(string home)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr nativePath = IntPtr.Zero;

            try
            {
                int result = SHGetKnownFolderPath(s_downloadsFolderId, 0u, IntPtr.Zero, out nativePath);

                if (result == 0 && nativePath != IntPtr.Zero)
                {
                    string path = Marshal.PtrToStringUni(nativePath);

                    if (!string.IsNullOrWhiteSpace(path))
                        return path;
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException)
            {
                // Fall through to the conventional home directory.
            }
            finally
            {
                if (nativePath != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(nativePath);
            }
#endif
            return HomeChild(home, "Downloads");
        }
    }
}
