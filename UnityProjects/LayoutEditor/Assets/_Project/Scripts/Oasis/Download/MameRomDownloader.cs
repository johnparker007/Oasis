using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Oasis.Download
{
    public class MameRomDownloader : MonoBehaviour
    {
        private const string DownloadRootUrl = "https://archive.org/download/CentralArquivistaArcade/";

        private static MameRomDownloader _instance;

        public static MameRomDownloader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var existing = FindObjectOfType<MameRomDownloader>();
                    if (existing != null)
                    {
                        _instance = existing;
                    }
                    else
                    {
                        var gameObject = new GameObject(nameof(MameRomDownloader));
                        _instance = gameObject.AddComponent<MameRomDownloader>();
                    }

                    if (_instance != null)
                    {
                        DontDestroyOnLoad(_instance.gameObject);
                    }
                }

                return _instance;
            }
        }

        public async Task<string> DownloadRomAsync(string romName)
        {
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
            throw new PlatformNotSupportedException("MAME ROM downloader currently supports only Windows builds.");
#else
            if (string.IsNullOrWhiteSpace(romName))
            {
                throw new ArgumentException("ROM name must be provided.", nameof(romName));
            }

            var trimmedRomName = romName.Trim();
            var downloadsRoot = GetRomDownloadDirectory();
            Directory.CreateDirectory(downloadsRoot);

            var archiveFileName = string.Format("{0}.zip", trimmedRomName);
            var archivePath = Path.Combine(downloadsRoot, archiveFileName);

            if (!File.Exists(archivePath))
            {
                var downloadUrl = string.Format("{0}{1}", DownloadRootUrl, archiveFileName);
                await DownloadUtility.DownloadFileAsync(downloadUrl, archivePath);
            }

            return archivePath;
#endif
        }

        public static string GetRomDownloadDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "Downloads", "MAME", "ROMs");
        }
    }
}
