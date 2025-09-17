using System;
using System.IO;
using System.Threading.Tasks;
using SevenZip;
using UnityEngine;

namespace LazyJedi.SevenZip
{
    public static class LazyExtractor
    {
        #region FIELDS

        /// <summary>
        /// Do Not Delete this String or Change the 7Zip Folder Path
        /// </summary>
        //private const string SevenZipDLL = @"Assets/Plugins/SevenZipSharp/7Zip/7z.dll";
        private const string SevenZipDLL = @"Assets/OasisPackages/ThirdParty/lazy-seven-zip-main/Assets/Plugins/SevenZipSharp/7Zip/7z.dll";

        private static readonly string TemporaryFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Temporary");

        #endregion

        #region CONSTRUCTORS

        static LazyExtractor()
        {
            SevenZipBase.SetLibraryPath(SevenZipDLL);
            if (Directory.Exists(TemporaryFolderPath)) return;
            Directory.CreateDirectory(TemporaryFolderPath);
        }

        #endregion

        #region NON-ASYNC METHODS

        /// <summary>
        /// Extract an Archive.<br/>
        /// If no OutPath is provided, the files of the Archive will be extracted to the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outPath">Output Path of the Extracted Files</param>
        /// <param name="inArchive">Input Archive File Path</param>
        /// <param name="password">Optional Password of the Archive if it Encrypted</param>
        public static void Extract(string outPath, string inArchive, string password = "")
        {
            if (string.IsNullOrEmpty(inArchive))
            {
                Debug.LogWarning("Invalid archive.");
                return;
            }

            if (string.IsNullOrEmpty(outPath)) outPath = Path.Combine(TemporaryFolderPath, Path.GetFileNameWithoutExtension(inArchive));
            if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);

            using SevenZipExtractor extractor = new SevenZipExtractor(inArchive, password);
            extractor.ExtractArchive(outPath);
        }

        #endregion

        #region ASYNC METHODS

        /// <summary>
        /// Extract an Archive.<br/>
        /// If no OutPath is provided, the files of the Archive will be extracted to the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outPath">Output Path of the Extracted Files</param>
        /// <param name="inArchive">Input Archive File Path</param>
        /// <param name="password">Optional Password of the Archive if it Encrypted</param>
        public static async Task ExtractAsync(string outPath, string inArchive, string password = "")
        {
            if (string.IsNullOrEmpty(inArchive))
            {
                Debug.LogWarning("Invalid archive.");
                return;
            }

            if (string.IsNullOrEmpty(outPath)) outPath = Path.Combine(TemporaryFolderPath, Path.GetFileNameWithoutExtension(inArchive));
            if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);

            using SevenZipExtractor extractor = new SevenZipExtractor(inArchive, password);
            await extractor.ExtractArchiveAsync(outPath);
        }

        #endregion

        #region ROOTED EXTRACTION

        public static void ExtractToRootedPath(string rootPath, string inArchive, string password = "")
        {
            if (string.IsNullOrEmpty(inArchive))
            {
                Debug.LogWarning("Invalid archive.");
                return;
            }

            if (string.IsNullOrEmpty(rootPath))
            {
                Debug.LogWarning("Invalid extraction root.");
                return;
            }

            string normalizedRootPath = Path.GetFullPath(rootPath);
            if (!Directory.Exists(normalizedRootPath)) Directory.CreateDirectory(normalizedRootPath);

            string rootComparisonPrefix = normalizedRootPath;
            if (!IsEndingWithDirectorySeparator(rootComparisonPrefix))
            {
                rootComparisonPrefix += Path.DirectorySeparatorChar;
            }

            using SevenZipExtractor extractor = new SevenZipExtractor(inArchive, password);
            foreach (ArchiveFileInfo entry in extractor.ArchiveFileData)
            {
                if (entry.IsDirectory) continue;

                string destinationCandidate = Path.Combine(normalizedRootPath, entry.FileName);

                string fullDestinationPath;
                try
                {
                    fullDestinationPath = Path.GetFullPath(destinationCandidate);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                {
                    Debug.LogWarning($"Skipping entry '{entry.FileName}' because its destination path is invalid: {ex.Message}");
                    continue;
                }

                if (!fullDestinationPath.StartsWith(rootComparisonPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"Skipping entry '{entry.FileName}' because it resolves outside of the extraction root.");
                    continue;
                }

                string destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                using FileStream destinationStream = new FileStream(
                    fullDestinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.SequentialScan);
                extractor.ExtractFile(entry.Index, destinationStream);
            }
        }

        private static bool IsEndingWithDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            char lastChar = path[path.Length - 1];
            return lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar;
        }

        #endregion
    }
}