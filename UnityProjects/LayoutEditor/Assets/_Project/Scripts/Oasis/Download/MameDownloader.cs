using System;
using System.IO;
using System.Threading.Tasks;
using LazyJedi.SevenZip;
using UnityEngine;
using UnityEngine.Networking;

namespace Oasis.Download
{
    public class MameDownloader : MonoBehaviour
    {
        public const string DefaultVersionNumber = "239";

        private const string DownloadRootUrl = "https://github.com/mamedev/mame/releases/download";

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
            var archiveFileName = string.Format("{0}b_64bit.exe", versionFolder);
            var archivePath = Path.Combine(downloadsRoot, archiveFileName);

            Directory.CreateDirectory(downloadsRoot);

            if (!File.Exists(archivePath))
            {
                var downloadUrl = string.Format("{0}/{1}/{2}", DownloadRootUrl, versionFolder, archiveFileName);
                await DownloadFileAsync(downloadUrl, archivePath);
            }

            Directory.CreateDirectory(extractPath);
            await LazyExtractor.ExtractAsync(extractPath, archivePath);

            return extractPath;
#endif
        }

        private static async Task DownloadFileAsync(string url, string destinationPath)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(destinationPath);
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    throw new InvalidOperationException(string.Format("Failed to download MAME from '{0}': {1}", url, request.error));
                }
            }
        }
    }
}
