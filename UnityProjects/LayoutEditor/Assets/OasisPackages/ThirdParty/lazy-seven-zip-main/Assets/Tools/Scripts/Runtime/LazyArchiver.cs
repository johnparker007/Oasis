using System.IO;
using System.Threading.Tasks;
using SevenZip;
using UnityEngine;
using CompressionLevel = SevenZip.CompressionLevel;

namespace LazyJedi.SevenZip
{
    public static class LazyArchiver
    {
        #region FIELDS

        /// <summary>
        /// Do Not Delete this String or Change the 7Zip Folder Path
        /// </summary>
        //private const string SevenZipDLL = @"Assets/Plugins/SevenZipSharp/7Zip/7z.dll";
        private const string SevenZipDLL = @"Assets/OasisPackages/ThirdParty/lazy-seven-zip-main/Assets/Plugins/SevenZipSharp/7Zip/7z.dll";

        private static readonly string TemporaryFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Temporary", "Archives");
        private static readonly string TemporaryArchivePath = Path.Combine(TemporaryFolderPath, "archive-");
          
        #endregion

        #region CONSTRUCTOR

        static LazyArchiver()
        {
            SevenZipBase.SetLibraryPath(SevenZipDLL);
            if (Directory.Exists(TemporaryFolderPath)) return;
            Directory.CreateDirectory(TemporaryFolderPath);
        }

        #endregion

        #region NON-ASYNC METHODS

        /// <summary>
        /// Create a non encrypted archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created.</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="compressionLevel"></param>
        public static void Archive(
            string outArchive,
            string[] inFiles,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            OutArchiveFormat archiveFormat = LazySevenZipHelper.ArchiveExtensionToFormat(Path.GetExtension(outArchive));
            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor =
                CreateCompressor(archiveFormat, compressionLevel: compressionLevel, directoryStructure: true, preserveRootDirectory: true);
            foreach (string file in inFiles)
            {
                CompressHelper(compressor, outArchive, file);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        /// <summary>
        /// Create a non encrypted archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="archiveFormat">Format of the Archive that will be created.</param>
        /// <param name="compressionLevel">Level of the Compression, by Default the Compression is set to Default.</param>
        public static void Archive(
            string outArchive,
            string[] inFiles,
            OutArchiveFormat archiveFormat,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor =
                CreateCompressor(archiveFormat, compressionLevel: compressionLevel, directoryStructure: true, preserveRootDirectory: true);
            foreach (string file in inFiles)
            {
                CompressHelper(compressor, outArchive, file);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        /// <summary>
        /// Create an Encrypted Archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="password">Password for the Encryption</param>
        /// <param name="encryptHeaders">Encrypt the Filenames</param>
        /// <param name="encryptionMethod">Type of encryption to Apply</param>
        /// <param name="compressionLevel">Level of the Compression, by Default the Compression is set to Default.</param>
        public static void Archive(
            string outArchive,
            string[] inFiles,
            string password,
            bool encryptHeaders = false,
            ZipEncryptionMethod encryptionMethod = ZipEncryptionMethod.Aes128,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            OutArchiveFormat archiveFormat = LazySevenZipHelper.ArchiveExtensionToFormat(Path.GetExtension(outArchive));
            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor = CreateCompressor(archiveFormat, encryptHeaders, encryptionMethod, compressionLevel, true, true);
            foreach (string file in inFiles)
            {
                CompressHelper(compressor, outArchive, file, password);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        /// <summary>
        /// Create an Encrypted Archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="password">Password for the Encryption</param>
        /// <param name="archiveFormat">Format of the Archive that will be created.</param>
        /// <param name="encryptHeaders">Encrypt the Filenames</param>
        /// <param name="encryptionMethod">Type of encryption to Apply</param>
        /// <param name="compressionLevel">Level of the Compression, by Default the Compression is set to Default.</param>
        public static void Archive(
            string outArchive,
            string[] inFiles,
            string password,
            OutArchiveFormat archiveFormat,
            bool encryptHeaders = false,
            ZipEncryptionMethod encryptionMethod = ZipEncryptionMethod.Aes128,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor = CreateCompressor(archiveFormat, encryptHeaders, encryptionMethod, compressionLevel, true, true);
            foreach (string file in inFiles)
            {
                CompressHelper(compressor, outArchive, file, password);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        #endregion

        #region ASYNC METHODS

        /// <summary>
        /// Create a Non-Encrypted Archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="compressionLevel">Level of the Compression, by Default the Compression is set to Default.</param>
        public static async Task ArchiveAsync(
            string outArchive,
            string[] inFiles,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            OutArchiveFormat archiveFormat = LazySevenZipHelper.ArchiveExtensionToFormat(Path.GetExtension(outArchive));
            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor =
                CreateCompressor(archiveFormat, compressionLevel: compressionLevel, directoryStructure: true, preserveRootDirectory: true);

            foreach (string file in inFiles)
            {
                await CompressHelperAsync(compressor, outArchive, file);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        /// <summary>
        /// Create a Non-Encrypted Archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="archiveFormat">Format of the Archive that will be created.</param>
        /// <param name="compressionLevel">Level of the Compression, by Default the Compression is set to Default.</param>
        public static async Task ArchiveAsync(
            string outArchive,
            string[] inFiles,
            OutArchiveFormat archiveFormat,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor =
                CreateCompressor(archiveFormat, compressionLevel: compressionLevel, directoryStructure: true, preserveRootDirectory: true);

            foreach (string file in inFiles)
            {
                await CompressHelperAsync(compressor, outArchive, file);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        /// <summary>
        /// Create an Encrypted Archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="password">Password for the Encryption</param>
        /// <param name="encryptHeaders">Encrypt the Filenames</param>
        /// <param name="encryptionMethod">Type of encryption to Apply</param>
        /// <param name="compressionLevel">Level of the Compression, by Default the Compression is set to Default.</param>
        public static async Task ArchiveAsync(
            string outArchive,
            string[] inFiles,
            string password,
            bool encryptHeaders = false,
            ZipEncryptionMethod encryptionMethod = ZipEncryptionMethod.Aes128,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            OutArchiveFormat archiveFormat = LazySevenZipHelper.ArchiveExtensionToFormat(Path.GetExtension(outArchive));
            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor = CreateCompressor(archiveFormat, encryptHeaders, encryptionMethod, compressionLevel, true, true);
            foreach (string file in inFiles)
            {
                await CompressHelperAsync(compressor, outArchive, file, password);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        /// <summary>
        /// Create an Encrypted Archive.<br/>
        /// If no OutArchive Path is provided, an Archive will be Created in the Temporary Folder within this Project.
        /// </summary>
        /// <param name="outArchive">Path of the Archive that will be Created</param>
        /// <param name="inFiles">List of Files and Folders to Archive</param>
        /// <param name="password">Password for the Encryption</param>
        /// <param name="archiveFormat">Format of the Archive (.7z, .zip, etc)</param>
        /// <param name="encryptHeaders">Encrypt the Filenames</param>
        /// <param name="encryptionMethod">Type of encryption to Apply</param>
        /// <param name="compressionLevel">Level of the Compression, by Default the Compression is set to Default.</param>
        public static async Task ArchiveAsync(
            string outArchive,
            string[] inFiles,
            string password,
            OutArchiveFormat archiveFormat,
            bool encryptHeaders = false,
            ZipEncryptionMethod encryptionMethod = ZipEncryptionMethod.Aes128,
            CompressionLevel compressionLevel = CompressionLevel.Normal)
        {
            if (inFiles.Length == 0)
            {
                Debug.LogError("There are no files to archive.");
                return;
            }

            if (string.IsNullOrEmpty(outArchive))
            {
                outArchive = NewArchive(archiveFormat);
                Debug.Log($"An invalid output path was provided, a temporary path will be used:\n{outArchive}");
            }

            SevenZipCompressor compressor = CreateCompressor(archiveFormat, encryptHeaders, encryptionMethod, compressionLevel, true, true);
            foreach (string file in inFiles)
            {
                await CompressHelperAsync(compressor, outArchive, file, password);
                if (compressor.CompressionMode == CompressionMode.Append) continue;
                compressor.CompressionMode = CompressionMode.Append;
            }
        }

        #endregion

        #region HELPER METHODS

        private static SevenZipCompressor CreateCompressor(
            OutArchiveFormat archiveFormat = OutArchiveFormat.Zip,
            bool encryptHeaders = false,
            ZipEncryptionMethod encryptionMethod = ZipEncryptionMethod.Aes128,
            CompressionLevel compressionLevel = CompressionLevel.Normal,
            bool directoryStructure = false,
            bool preserveRootDirectory = false
        )
        {
            return new SevenZipCompressor(TemporaryFolderPath)
            {
                ArchiveFormat         = archiveFormat,
                EncryptHeaders        = encryptHeaders,
                ZipEncryptionMethod   = encryptionMethod,
                CompressionLevel      = compressionLevel,
                DirectoryStructure    = directoryStructure,
                PreserveDirectoryRoot = preserveRootDirectory
            };
        }

        private static void CompressHelper(SevenZipCompressor compressor, string outArchive, string inFile)
        {
            if (File.GetAttributes(inFile).HasFlag(FileAttributes.Directory))
            {
                compressor.CompressDirectory(inFile, outArchive);
            }
            else
            {
                compressor.CompressFiles(outArchive, inFile);
            }
        }

        private static void CompressHelper(SevenZipCompressor compressor, string outArchive, string inFile, string password)
        {
            if (File.GetAttributes(inFile).HasFlag(FileAttributes.Directory))
            {
                compressor.CompressDirectory(inFile, outArchive, password);
            }
            else
            {
                compressor.CompressFilesEncrypted(outArchive, password, inFile);
            }
        }

        private static async Task CompressHelperAsync(SevenZipCompressor compressor, string outArchive, string inFile)
        {
            if (File.GetAttributes(inFile).HasFlag(FileAttributes.Directory))
            {
                await compressor.CompressDirectoryAsync(inFile, outArchive);
            }
            else
            {
                await compressor.CompressFilesAsync(outArchive, inFile);
            }
        }

        private static async Task CompressHelperAsync(SevenZipCompressor compressor, string outArchive, string inFile, string password)
        {
            if (File.GetAttributes(inFile).HasFlag(FileAttributes.Directory))
            {
                await compressor.CompressDirectoryAsync(inFile, outArchive, password);
            }
            else
            {
                await compressor.CompressFilesEncryptedAsync(outArchive, password, inFile);
            }
        }

        private static string NewArchive(OutArchiveFormat archiveFormat)
        {
            return
                $"{TemporaryArchivePath}{Directory.GetFiles(TemporaryFolderPath, "*.meta").Length}{LazySevenZipHelper.ArchiveFormatToExtension(archiveFormat)}";
        }

        #endregion
    }
}