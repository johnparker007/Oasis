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

            outPath = PrepareOutputPath(outPath, inArchive);

            ExtractInternal(outPath, inArchive, password);
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

            outPath = PrepareOutputPath(outPath, inArchive);

            await Task.Run(() => ExtractInternal(outPath, inArchive, password));
        }

        private static string PrepareOutputPath(string outPath, string inArchive)
        {
            if (string.IsNullOrEmpty(outPath))
            {
                outPath = Path.Combine(TemporaryFolderPath, Path.GetFileNameWithoutExtension(inArchive));
            }

            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }

            return outPath;
        }

        private static void ExtractInternal(string outPath, string inArchive, string password)
        {
            using SevenZipExtractor extractor = new SevenZipExtractor(inArchive, password);

            if (Path.IsPathRooted(outPath))
            {
                ExtractToRootedPath(extractor, outPath);
            }
            else
            {
                extractor.ExtractArchive(outPath);
            }
        }

        private static void ExtractToRootedPath(SevenZipExtractor extractor, string outPath)
        {
            string rootPath = Path.GetFullPath(outPath);
            string rootWithSeparator = EnsureTrailingSeparator(rootPath);

            foreach (ArchiveFileInfo fileInfo in extractor.ArchiveFileData)
            {
                if (string.IsNullOrEmpty(fileInfo.FileName)) continue;

                string destinationPath = Path.Combine(rootPath, fileInfo.FileName);
                string fullDestinationPath = Path.GetFullPath(destinationPath);

                if (!fullDestinationPath.StartsWith(rootWithSeparator, System.StringComparison.OrdinalIgnoreCase) && !string.Equals(fullDestinationPath, rootPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"Skipping extraction of '{fileInfo.FileName}' because it resolves outside of '{rootPath}'.");
                    continue;
                }

                if (fileInfo.IsDirectory)
                {
                    if (!Directory.Exists(fullDestinationPath))
                    {
                        Directory.CreateDirectory(fullDestinationPath);
                    }

                    continue;
                }

                string? directory = Path.GetDirectoryName(fullDestinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using FileStream fileStream = new FileStream(fullDestinationPath, FileMode.Create, FileAccess.Write);
                extractor.ExtractFile(fileInfo.Index, fileStream);
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            char separator = Path.DirectorySeparatorChar;

            if (!path.EndsWith(separator.ToString()))
            {
                path += separator;
            }

            return path;
        }

        #endregion
    }
}