using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UpmPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NowUI.Editor
{
    /// <summary>
    /// Explicitly installs the NowUI coding-agent skill into the current project.
    /// Nothing runs automatically when the package is imported or updated.
    /// </summary>
    internal static class NowUIAgentSkillInstaller
    {
        const string MenuPath = "NowUI/AI/Install Agent Skill";
        const string CopyInstructionsMenuPath = "NowUI/AI/Copy Project AGENTS.md Snippet";
        const string PackageName = "com.blenminer.nowui";
        const string SkillRelativePath = "AI~/skills/nowui";
        const string InstructionsRelativePath = "AI~/AGENTS.snippet.md";
        const string DestinationRelativePath = ".agents/skills/nowui";
        const string ReceiptFileName = ".nowui-install.json";

        [Serializable]
        sealed class PackageManifest
        {
            public string name;
            public string version;
        }

        [Serializable]
        sealed class InstallReceipt
        {
            public string packageName;
            public string packageVersion;
            public string contentHash;
        }

        [MenuItem(MenuPath)]
        static void InstallAgentSkill()
        {
            try
            {
                string packageRoot = ResolvePackageRoot(out string packageVersion);
                if (string.IsNullOrEmpty(packageRoot))
                {
                    ShowError(
                        "Could not locate the installed NowUI package. " +
                        "The installer supports source checkouts, embedded packages, and UPM PackageCache installs.");
                    return;
                }

                string sourceRoot = Path.GetFullPath(Path.Combine(packageRoot, SkillRelativePath));
                string skillEntryPoint = Path.Combine(sourceRoot, "SKILL.md");
                if (!Directory.Exists(sourceRoot) || !File.Exists(skillEntryPoint))
                {
                    ShowError(
                        "The packaged NowUI agent skill is missing. Expected to find:\n\n" +
                        skillEntryPoint);
                    return;
                }

                string projectRoot = GetProjectRoot();
                string destinationRoot = Path.GetFullPath(Path.Combine(projectRoot, DestinationRelativePath));
                string sourceHash = HashDirectory(sourceRoot);

                bool destinationExists = Directory.Exists(destinationRoot);
                bool isUpdate = false;

                if (destinationExists)
                {
                    string destinationHash = HashDirectory(destinationRoot);
                    if (string.Equals(destinationHash, sourceHash, StringComparison.Ordinal))
                    {
                        WriteReceipt(destinationRoot, packageVersion, sourceHash);
                        ShowSuccess(
                            "The NowUI agent skill is already up to date.\n\n" +
                            destinationRoot);
                        return;
                    }

                    InstallReceipt receipt = ReadReceipt(destinationRoot);
                    bool isKnownUnmodifiedInstall =
                        receipt != null &&
                        string.Equals(receipt.packageName, PackageName, StringComparison.Ordinal) &&
                        !string.IsNullOrEmpty(receipt.contentHash) &&
                        string.Equals(destinationHash, receipt.contentHash, StringComparison.Ordinal);

                    if (!isKnownUnmodifiedInstall)
                    {
                        bool reveal = EditorUtility.DisplayDialog(
                            "NowUI Agent Skill Was Not Replaced",
                            "An existing skill differs from the copy previously installed by NowUI, " +
                            "or has no NowUI install receipt. It may contain local changes, so the installer " +
                            "left it untouched.\n\nMove or remove the existing folder, then run the menu command again.\n\n" +
                            destinationRoot,
                            "Reveal Existing Skill",
                            "Close");

                        if (reveal)
                            EditorUtility.RevealInFinder(destinationRoot);
                        return;
                    }

                    isUpdate = true;
                }

                string destinationParent = Path.GetDirectoryName(destinationRoot);
                if (string.IsNullOrEmpty(destinationParent))
                    throw new InvalidOperationException("Could not resolve the agent skill destination folder.");

                Directory.CreateDirectory(destinationParent);
                string stagingRoot = Path.Combine(
                    destinationParent,
                    ".nowui-install-" + Guid.NewGuid().ToString("N"));

                string retainedBackup = null;
                try
                {
                    CopyDirectory(sourceRoot, stagingRoot);
                    WriteReceipt(stagingRoot, packageVersion, sourceHash);
                    retainedBackup = CommitStagedInstall(stagingRoot, destinationRoot, destinationExists);
                    stagingRoot = null;
                }
                finally
                {
                    if (!string.IsNullOrEmpty(stagingRoot) && Directory.Exists(stagingRoot))
                        Directory.Delete(stagingRoot, true);
                }

                // The destination is outside Assets and starts with '.', so Unity does not import it.
                // An AssetDatabase refresh would only add unnecessary project-wide work.
                string result = isUpdate ? "updated" : "installed";
                string message =
                    "The NowUI agent skill was " + result + " successfully.\n\n" +
                    "Destination:\n" + destinationRoot;

                if (!string.IsNullOrEmpty(retainedBackup))
                {
                    message +=
                        "\n\nThe previous unmodified copy could not be deleted and was retained at:\n" +
                        retainedBackup;
                }

                Debug.Log("NowUI agent skill " + result + " at " + destinationRoot);
                ShowSuccess(message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowError(
                    "The NowUI agent skill could not be installed. No modified skill was overwritten.\n\n" +
                    exception.Message);
            }
        }

        [MenuItem(CopyInstructionsMenuPath)]
        static void CopyProjectInstructions()
        {
            try
            {
                string packageRoot = ResolvePackageRoot(out _);
                if (string.IsNullOrEmpty(packageRoot))
                {
                    ShowError("Could not locate the installed NowUI package.");
                    return;
                }

                string snippetPath = Path.GetFullPath(Path.Combine(packageRoot, InstructionsRelativePath));
                if (!File.Exists(snippetPath))
                {
                    ShowError(
                        "The packaged NowUI project-instructions snippet is missing. Expected to find:\n\n" +
                        snippetPath);
                    return;
                }

                EditorGUIUtility.systemCopyBuffer = File.ReadAllText(snippetPath);
                EditorUtility.DisplayDialog(
                    "NowUI Project Instructions",
                    "The NowUI guidance block was copied to the clipboard.\n\n" +
                    "Paste it into AGENTS.md at the Unity project root. Create that file if it does not exist. " +
                    "This command does not change project files automatically.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowError("The NowUI project-instructions snippet could not be copied.\n\n" + exception.Message);
            }
        }

        static string ResolvePackageRoot(out string packageVersion)
        {
            packageVersion = string.Empty;

            try
            {
                UpmPackageInfo assemblyPackage = UpmPackageInfo.FindForAssembly(typeof(NowUIAgentSkillInstaller).Assembly);
                if (assemblyPackage != null &&
                    string.Equals(assemblyPackage.name, PackageName, StringComparison.Ordinal) &&
                    TryUsePackageRoot(assemblyPackage.resolvedPath, out packageVersion))
                {
                    return Path.GetFullPath(assemblyPackage.resolvedPath);
                }

                UpmPackageInfo[] registeredPackages = UpmPackageInfo.GetAllRegisteredPackages();
                if (registeredPackages != null)
                {
                    for (int i = 0; i < registeredPackages.Length; i++)
                    {
                        UpmPackageInfo package = registeredPackages[i];
                        if (package != null &&
                            string.Equals(package.name, PackageName, StringComparison.Ordinal) &&
                            TryUsePackageRoot(package.resolvedPath, out packageVersion))
                        {
                            return Path.GetFullPath(package.resolvedPath);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("NowUI could not query Package Manager metadata: " + exception.Message);
            }

            string callerPath = GetThisSourceFilePath();
            if (!string.IsNullOrEmpty(callerPath))
            {
                if (!Path.IsPathRooted(callerPath))
                    callerPath = Path.Combine(GetProjectRoot(), callerPath);

                string sourceRoot = FindPackageRootAbove(Path.GetFullPath(callerPath), out packageVersion);
                if (!string.IsNullOrEmpty(sourceRoot))
                    return sourceRoot;
            }

            string projectRoot = GetProjectRoot();
            string[] conventionalRoots =
            {
                Path.Combine(projectRoot, "Assets", "NowUI"),
                Path.Combine(projectRoot, "Packages", PackageName)
            };

            for (int i = 0; i < conventionalRoots.Length; i++)
            {
                if (TryUsePackageRoot(conventionalRoots[i], out packageVersion))
                    return Path.GetFullPath(conventionalRoots[i]);
            }

            string packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
            if (Directory.Exists(packageCache))
            {
                string[] cachedRoots = Directory.GetDirectories(packageCache, PackageName + "@*");
                Array.Sort(cachedRoots, StringComparer.Ordinal);
                for (int i = cachedRoots.Length - 1; i >= 0; i--)
                {
                    if (TryUsePackageRoot(cachedRoots[i], out packageVersion))
                        return Path.GetFullPath(cachedRoots[i]);
                }
            }

            return null;
        }

        static string FindPackageRootAbove(string sourceFilePath, out string packageVersion)
        {
            packageVersion = string.Empty;
            DirectoryInfo directory = new FileInfo(sourceFilePath).Directory;
            while (directory != null)
            {
                if (TryUsePackageRoot(directory.FullName, out packageVersion))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }

        static bool TryUsePackageRoot(string candidate, out string packageVersion)
        {
            packageVersion = string.Empty;
            if (string.IsNullOrEmpty(candidate) || !Directory.Exists(candidate))
                return false;

            string manifestPath = Path.Combine(candidate, "package.json");
            if (!File.Exists(manifestPath))
                return false;

            try
            {
                PackageManifest manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || !string.Equals(manifest.name, PackageName, StringComparison.Ordinal))
                    return false;

                packageVersion = manifest.version ?? string.Empty;
                return true;
            }
            catch
            {
                return false;
            }
        }

        static string GetProjectRoot()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Could not resolve the Unity project root.");

            return Path.GetFullPath(projectRoot);
        }

        static string GetThisSourceFilePath([CallerFilePath] string sourceFilePath = "")
        {
            return sourceFilePath;
        }

        static InstallReceipt ReadReceipt(string skillRoot)
        {
            string receiptPath = Path.Combine(skillRoot, ReceiptFileName);
            if (!File.Exists(receiptPath))
                return null;

            try
            {
                return JsonUtility.FromJson<InstallReceipt>(File.ReadAllText(receiptPath));
            }
            catch
            {
                return null;
            }
        }

        static void WriteReceipt(string skillRoot, string packageVersion, string contentHash)
        {
            var receipt = new InstallReceipt
            {
                packageName = PackageName,
                packageVersion = packageVersion ?? string.Empty,
                contentHash = contentHash
            };

            string json = JsonUtility.ToJson(receipt, true) + Environment.NewLine;
            File.WriteAllText(
                Path.Combine(skillRoot, ReceiptFileName),
                json,
                new UTF8Encoding(false));
        }

        static string HashDirectory(string root)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            string[] files = Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);

            var index = new StringBuilder();
            for (int i = 0; i < files.Length; i++)
            {
                string relativePath = files[i].Substring(prefix.Length).Replace('\\', '/');
                if (string.Equals(relativePath, ReceiptFileName, StringComparison.Ordinal))
                    continue;

                byte[] fileHash;
                using (var file = File.OpenRead(files[i]))
                using (var sha = SHA256.Create())
                    fileHash = sha.ComputeHash(file);

                index.Append(relativePath.Length)
                    .Append(':')
                    .Append(relativePath)
                    .Append(':')
                    .Append(Convert.ToBase64String(fileHash))
                    .Append('\n');
            }

            byte[] indexBytes = Encoding.UTF8.GetBytes(index.ToString());
            using (var sha = SHA256.Create())
                return BytesToHex(sha.ComputeHash(indexBytes));
        }

        static string BytesToHex(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString("x2"));
            return result.ToString();
        }

        static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            Directory.CreateDirectory(destinationRoot);
            string fullSource = Path.GetFullPath(sourceRoot).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string prefix = fullSource + Path.DirectorySeparatorChar;

            string[] directories = Directory.GetDirectories(fullSource, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                string relativePath = directories[i].Substring(prefix.Length);
                Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath));
            }

            string[] files = Directory.GetFiles(fullSource, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relativePath = files[i].Substring(prefix.Length);
                if (string.Equals(relativePath.Replace('\\', '/'), ReceiptFileName, StringComparison.Ordinal))
                    continue;

                string destinationPath = Path.Combine(destinationRoot, relativePath);
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);
                File.Copy(files[i], destinationPath, true);
            }
        }

        static string CommitStagedInstall(
            string stagingRoot,
            string destinationRoot,
            bool destinationExists)
        {
            if (!destinationExists)
            {
                Directory.Move(stagingRoot, destinationRoot);
                return null;
            }

            string destinationParent = Path.GetDirectoryName(destinationRoot);
            if (string.IsNullOrEmpty(destinationParent))
                throw new InvalidOperationException("Could not resolve the agent skill destination folder.");

            string backupRoot = Path.Combine(
                destinationParent,
                ".nowui-backup-" + Guid.NewGuid().ToString("N"));

            Directory.Move(destinationRoot, backupRoot);
            try
            {
                Directory.Move(stagingRoot, destinationRoot);
            }
            catch
            {
                if (!Directory.Exists(destinationRoot) && Directory.Exists(backupRoot))
                    Directory.Move(backupRoot, destinationRoot);
                throw;
            }

            try
            {
                Directory.Delete(backupRoot, true);
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "NowUI updated the agent skill, but could not delete its temporary backup at " +
                    backupRoot + ": " + exception.Message);
                return backupRoot;
            }
        }

        static void ShowSuccess(string message)
        {
            EditorUtility.DisplayDialog("NowUI Agent Skill", message, "OK");
        }

        static void ShowError(string message)
        {
            Debug.LogError(message);
            EditorUtility.DisplayDialog("NowUI Agent Skill Installation Failed", message, "OK");
        }
    }
}
