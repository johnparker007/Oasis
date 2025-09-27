using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Oasis.Utility;

namespace Oasis.Download
{
    public class MameDownloader : MonoBehaviour
    {
        public const string DefaultVersionNumber = "281";

        private const string DownloadRootUrl = "https://github.com/mamedev/mame/releases/download";
        private const string SevenZipExecutableName = "7z.exe";
        private const string SevenZipFolderName = "7-Zip";
        private static readonly string SevenZipExecutableRelativePath = Path.Combine(SevenZipFolderName, SevenZipExecutableName);

        private static MameDownloader _instance;

        public static MameDownloader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var existing = FindObjectOfType<MameDownloader>();
                    if (existing != null)
                    {
                        _instance = existing;
                    }
                    else
                    {
                        var gameObject = new GameObject(nameof(MameDownloader));
                        _instance = gameObject.AddComponent<MameDownloader>();
                    }

                    if (_instance != null)
                    {
                        DontDestroyOnLoad(_instance.gameObject);
                    }
                }

                return _instance;
            }
        }

        public async Task<string> DownloadAndExtractAsync(string versionNumber = DefaultVersionNumber)
        {
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
            throw new PlatformNotSupportedException("MAME downloader currently supports only Windows builds.");
#else
            if (string.IsNullOrWhiteSpace(versionNumber))
            {
                throw new ArgumentException("Version number must be provided.", nameof(versionNumber));
            }

            var paddedVersion = versionNumber.Trim().PadLeft(4, '0');
            var versionFolder = string.Format("mame{0}", paddedVersion);
            var downloadsRoot = Path.Combine(Application.persistentDataPath, "Downloads", "MAME");
            var extractPath = Path.Combine(downloadsRoot, versionFolder);
            var archiveFileName = string.Format("{0}b_x64.exe", versionFolder);
            var archivePath = Path.Combine(downloadsRoot, archiveFileName);

            Directory.CreateDirectory(downloadsRoot);

            if (!File.Exists(archivePath))
            {
                var downloadUrl = string.Format("{0}/{1}/{2}", DownloadRootUrl, versionFolder, archiveFileName);
                UnityEngine.Debug.LogError("downloadUrl: " + downloadUrl);
                await DownloadUtility.DownloadFileAsync(downloadUrl, archivePath);
            }

            Directory.CreateDirectory(extractPath);
            await ExtractArchiveAsync(archivePath, extractPath);
            CopyMamePlugins(extractPath);

            return extractPath;
#endif
        }

        private static async Task ExtractArchiveAsync(string archivePath, string extractPath)
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"Archive not found at '{archivePath}'.", archivePath);
            }

            string sevenZipPath = ExternalExecutableUtility.GetExecutablePath(SevenZipExecutableRelativePath);
            if (string.IsNullOrEmpty(sevenZipPath) || !File.Exists(sevenZipPath))
            {
                sevenZipPath = ExternalExecutableUtility.GetExecutablePath(SevenZipExecutableName);
            }
            if (string.IsNullOrEmpty(sevenZipPath) || !File.Exists(sevenZipPath))
            {
                throw new InvalidOperationException($"Required extractor '{SevenZipExecutableName}' was not found.");
            }

            string extractionArguments = $"x \"{archivePath}\" -o\"{extractPath}\" -y";

            var startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = extractionArguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(sevenZipPath) ?? string.Empty
            };

            await Task.Run(() =>
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new InvalidOperationException("Failed to start the 7z extraction process.");
                    }

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"7z extraction failed with exit code {process.ExitCode}.");
                    }
                }
            });
        }

        private static void CopyMamePlugins(string extractPath)
        {
            if (string.IsNullOrEmpty(extractPath))
            {
                throw new ArgumentException("Extract path must be provided.", nameof(extractPath));
            }

            string pluginSourceDirectory = GetPluginSourceDirectory();

            if (!Directory.Exists(pluginSourceDirectory))
            {
                UnityEngine.Debug.LogWarning($"MAME plugin source directory '{pluginSourceDirectory}' does not exist. Plugins will not be copied.");
                return;
            }

            string pluginDestinationRoot = Path.Combine(extractPath, "plugins");
            string pluginDestinationDirectory = Path.Combine(pluginDestinationRoot, "oasis");

            Directory.CreateDirectory(pluginDestinationRoot);

            if (Directory.Exists(pluginDestinationDirectory))
            {
                Directory.Delete(pluginDestinationDirectory, true);
            }

            CopyDirectory(pluginSourceDirectory, pluginDestinationDirectory);
            UnityEngine.Debug.Log($"Copied MAME plugins from '{pluginSourceDirectory}' to '{pluginDestinationDirectory}'.");
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                string destinationFilePath = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destinationFilePath, true);
            }

            foreach (string directoryPath in Directory.GetDirectories(sourceDir))
            {
                string directoryName = Path.GetFileName(directoryPath);
                if (string.IsNullOrEmpty(directoryName))
                {
                    continue;
                }

                string destinationSubDirectory = Path.Combine(destinationDir, directoryName);
                CopyDirectory(directoryPath, destinationSubDirectory);
            }
        }

        private static string GetPluginSourceDirectory()
        {
            string externalAssetsDirectory = ExternalExecutableUtility.GetExternalAssetsDirectory();

            if (string.IsNullOrEmpty(externalAssetsDirectory))
            {
                UnityEngine.Debug.LogWarning("External assets directory is not configured. MAME plugins will not be copied.");
                return string.Empty;
            }

            return Path.Combine(externalAssetsDirectory, "MameLuaPlugins", "oasis");
        }

    }
}
