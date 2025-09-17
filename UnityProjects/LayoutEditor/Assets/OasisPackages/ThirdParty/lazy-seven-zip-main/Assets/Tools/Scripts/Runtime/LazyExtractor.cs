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
    }
}