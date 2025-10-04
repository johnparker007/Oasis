using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Oasis.NativeProgress;
using Oasis.Utility;

namespace Oasis.Download
{
    public class MameDownloader : MonoBehaviour
    {
        public const int kDefaultVersionNumber = 281;

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

        public enum MameDownloadStage
        {
            Downloading,
            Extracting,
            InstallingPlugins
        }

        public async Task<string> DownloadAndExtractAsync(
            int versionNumber = kDefaultVersionNumber,
            Action<MameDownloadStage> onStageChanged = null,
            Action<long> onDownloadProgress = null,
            Action<float> onExtractionProgress = null)
        {
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
            throw new PlatformNotSupportedException("MAME downloader currently supports only Windows builds.");
#else
            string versionNumberString = versionNumber.ToString();

            var paddedVersion = versionNumberString.Trim().PadLeft(4, '0');
            var versionFolder = string.Format("mame{0}", paddedVersion);
            var downloadsRoot = Path.Combine(Application.persistentDataPath, "Downloads", "MAME");
            var extractPath = Path.Combine(downloadsRoot, versionFolder);

            string archiveFileName;
            if (versionNumber >= 281)
            {
                archiveFileName = string.Format("{0}b_x64.exe", versionFolder);

            }
            else
            {
                archiveFileName = string.Format("{0}b_64bit.exe", versionFolder);

            }
            var archivePath = Path.Combine(downloadsRoot, archiveFileName);

            Directory.CreateDirectory(downloadsRoot);

            onStageChanged?.Invoke(MameDownloadStage.Downloading);

            if (!File.Exists(archivePath))
            {
                var downloadUrl = string.Format("{0}/{1}/{2}", DownloadRootUrl, versionFolder, archiveFileName);
                UnityEngine.Debug.LogError("downloadUrl: " + downloadUrl);
                await DownloadUtility.DownloadFileAsync(downloadUrl, archivePath, onDownloadProgress);
            }
            else
            {
                onDownloadProgress?.Invoke(new FileInfo(archivePath).Length);
            }

            Directory.CreateDirectory(extractPath);

            onStageChanged?.Invoke(MameDownloadStage.Extracting);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Action<float> extractionProgressHandler = progress =>
            {
                float clampedProgress = Mathf.Clamp01(progress);
                onExtractionProgress?.Invoke(clampedProgress);

                int percentValue = Mathf.Clamp(Mathf.RoundToInt(clampedProgress * 100f), 0, 100);
                NativeProgressWindow.UpdateContent(
                    "Extracting MAME...",
                    $"Extracting MAME... {percentValue}%",
                    false);

                float overallProgress = Mathf.Lerp(0.5f, 0.75f, clampedProgress);
                NativeProgressWindow.UpdateProgress(overallProgress);
            };
#else
            Action<float> extractionProgressHandler = progress =>
            {
                float clampedProgress = Mathf.Clamp01(progress);
                onExtractionProgress?.Invoke(clampedProgress);
            };
#endif

            await ExtractArchiveAsync(archivePath, extractPath, extractionProgressHandler);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            NativeProgressWindow.UpdateContent("Extracting MAME...", "Extracting MAME...", false, 0.5f);
            NativeProgressWindow.UpdateProgress(0.5f);
#endif

            onStageChanged?.Invoke(MameDownloadStage.InstallingPlugins);
            CopyMamePlugins(extractPath);

            return extractPath;
#endif
        }

        private static async Task ExtractArchiveAsync(string archivePath, string extractPath, Action<float> onExtractionProgress = null)
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

            string extractionArguments = $"x \"{archivePath}\" -o\"{extractPath}\" -y -bsp1 -bse1";

            var startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = extractionArguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(sevenZipPath) ?? string.Empty
            };

            await Task.Run(() =>
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    if (!process.Start())
                    {
                        throw new InvalidOperationException("Failed to start the 7z extraction process.");
                    }

                    float lastReportedProgress = -1f;

                    void HandleProgress(string data)
                    {
                        float? parsedProgress = TryParseProgressValue(data);
                        if (!parsedProgress.HasValue)
                        {
                            return;
                        }

                        float normalized = parsedProgress.Value;

                        if (Math.Abs(normalized - lastReportedProgress) < 0.0001f)
                        {
                            return;
                        }

                        lastReportedProgress = normalized;
                        onExtractionProgress?.Invoke(normalized);
                    }

                    DataReceivedEventHandler outputHandler = (sender, args) => HandleProgress(args.Data);
                    DataReceivedEventHandler errorHandler = (sender, args) => HandleProgress(args.Data);

                    process.OutputDataReceived += outputHandler;
                    process.ErrorDataReceived += errorHandler;

                    try
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            throw new InvalidOperationException($"7z extraction failed with exit code {process.ExitCode}.");
                        }

                        if (lastReportedProgress < 1f)
                        {
                            lastReportedProgress = 1f;
                            onExtractionProgress?.Invoke(1f);
                        }
                    }
                    finally
                    {
                        try
                        {
                            process.CancelOutputRead();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        try
                        {
                            process.CancelErrorRead();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        process.OutputDataReceived -= outputHandler;
                        process.ErrorDataReceived -= errorHandler;
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

        private static float? TryParseProgressValue(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }

            var cleanedBuilder = new StringBuilder(data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                char c = data[i];
                if (c == '\r' || c == '\b' || c == '\u001b')
                {
                    continue;
                }

                cleanedBuilder.Append(c);
            }

            if (cleanedBuilder.Length == 0)
            {
                return null;
            }

            string cleaned = cleanedBuilder.ToString();

            int percentIndex = cleaned.LastIndexOf('%');
            if (percentIndex <= 0)
            {
                return null;
            }

            int valueStart = percentIndex - 1;
            while (valueStart >= 0)
            {
                char c = cleaned[valueStart];
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    valueStart--;
                    continue;
                }

                valueStart++;
                break;
            }

            if (valueStart < 0)
            {
                valueStart = 0;
            }

            int length = percentIndex - valueStart;
            if (length <= 0)
            {
                return null;
            }

            string numberString = cleaned.Substring(valueStart, length).Replace(',', '.');
            if (!float.TryParse(numberString, NumberStyles.Float, CultureInfo.InvariantCulture, out float percentValue))
            {
                return null;
            }

            float normalized = percentValue / 100f;
            if (normalized < 0f)
            {
                normalized = 0f;
            }
            else if (normalized > 1f)
            {
                normalized = 1f;
            }

            return normalized;
        }

    }
}
