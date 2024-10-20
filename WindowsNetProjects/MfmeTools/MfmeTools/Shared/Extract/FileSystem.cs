using Newtonsoft.Json;
using System.IO;

namespace Oasis.MfmeTools.Shared.Extract
{
    public static class FileSystem
    {
        public static readonly string kExtractRootDirectoryName = ".extract";
        public static readonly string kBackgroundDirectoryName = "background";
        public static readonly string kReelsDirectoryName = "reels";
        public static readonly string kLampsDirectoryName = "lamps";
        public static readonly string kButtonsDirectoryName = "buttons";
        public static readonly string kBitmapsDirectoryName = "bitmaps";
        public static readonly string kMiscDirectoryName = "misc";

        public static readonly string kRomIdentFilename = "romident.txt";


        private static string _sourceLayoutPath = null;
        private static string _targetExtractRootPath = null;

        public static bool UseCachedBackgroundImage
        {
            get;
            private set;
        }

        public static bool UseCachedReelImages
        {
            get;
            private set;
        }

        public static bool UseCachedLampImages
        {
            get;
            private set;
        }

        public static bool UseCachedButtonImages
        {
            get;
            private set;
        }

        public static bool UseCachedBitmapImages
        {
            get;
            private set;
        }

        public static bool UseCachedRomIdent
        {
            get;
            private set;
        }

        public static string RomIdentPath
        {
            get
            {
                return Path.Combine(_targetExtractRootPath, kMiscDirectoryName, kRomIdentFilename);
            }
        }

        public static void Setup(string sourceLayoutPath, 
            bool useCachedBackgroundImage,
            bool useCachedReelImages,
            bool useCachedLampImages,
            bool useCachedButtonImages,
            bool useCachedBitmapImages,
            bool useCachedRomIdent)
        {
            UseCachedBackgroundImage = useCachedBackgroundImage;
            UseCachedReelImages = useCachedReelImages;
            UseCachedLampImages = useCachedLampImages;
            UseCachedButtonImages = useCachedButtonImages;
            UseCachedBitmapImages = useCachedBitmapImages;
            UseCachedRomIdent = useCachedRomIdent;

            _sourceLayoutPath = sourceLayoutPath;
            _targetExtractRootPath = GetTargetExtractPathRoot(_sourceLayoutPath);

            CreateDirectories(_targetExtractRootPath);
        }

        public static void CreateDirectories(string targetExtractRootPath)
        {
            if (!Directory.Exists(targetExtractRootPath))
            {
                Directory.CreateDirectory(targetExtractRootPath);
            }

            CreateResourceDirectory(targetExtractRootPath, kBackgroundDirectoryName);
            CreateResourceDirectory(targetExtractRootPath, kReelsDirectoryName);
            CreateResourceDirectory(targetExtractRootPath, kLampsDirectoryName);
            CreateResourceDirectory(targetExtractRootPath, kButtonsDirectoryName);
            CreateResourceDirectory(targetExtractRootPath, kBitmapsDirectoryName);
            CreateResourceDirectory(targetExtractRootPath, kMiscDirectoryName);
        }

        public static string ReadRomIdent()
        {
            if(!File.Exists(RomIdentPath))
            {
                return null;
            }

            return File.ReadAllText(RomIdentPath);
        }

        public static void WriteRomIdent(string romIdent)
        {
            TryDeleteResource(RomIdentPath);
            File.WriteAllText(RomIdentPath, romIdent);
        }

        public static void TryDeleteBackgroundImage(string filename)
        {
            TryDeleteResource(_targetExtractRootPath, kBackgroundDirectoryName, filename);
        }

        public static string GetFullBackgroundImagePath(string filename)
        {
            return Path.Combine(_targetExtractRootPath, kBackgroundDirectoryName, filename);
        }

        public static void TryDeleteReelImage(string filename)
        {
            TryDeleteResource(_targetExtractRootPath, kReelsDirectoryName, filename);
        }

        public static string GetFullReelImagePath(string filename)
        {
            return Path.Combine(_targetExtractRootPath, kReelsDirectoryName, filename);
        }

        public static void TryDeleteLampImage(string filename)
        {
            TryDeleteResource(_targetExtractRootPath, kLampsDirectoryName, filename);
        }

        public static string GetFullLampImagePath(string filename)
        {
            return Path.Combine(_targetExtractRootPath, kLampsDirectoryName, filename);
        }

        public static void TryDeleteButtonImage(string filename)
        {
            TryDeleteResource(_targetExtractRootPath, kButtonsDirectoryName, filename);
        }

        public static string GetFullButtonImagePath(string filename)
        {
            return Path.Combine(_targetExtractRootPath, kButtonsDirectoryName, filename);
        }

        public static void SaveLayout(Layout layout)
        {
            string json = JsonConvert.SerializeObject(layout, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto //, ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            string filePath = Path.Combine(_targetExtractRootPath, layout.ASName + ".json");

            File.WriteAllText(filePath, json);
        }

        private static string GetTargetExtractPathRoot(string sourceLayoutPath)
        {
            string sourceLayoutDirectoryPath = Path.GetDirectoryName(sourceLayoutPath);
            string targetExtractRootPath = Path.Combine(sourceLayoutDirectoryPath, kExtractRootDirectoryName);

            return targetExtractRootPath;
        }

        private static void CreateResourceDirectory(string targetExtractRootPath, string imageDirectoryName)
        {
            string imageDirectoryPath = Path.Combine(targetExtractRootPath, imageDirectoryName);
            if (!Directory.Exists(imageDirectoryPath))
            {
                Directory.CreateDirectory(imageDirectoryPath);
            }
        }

        private static void TryDeleteResource(string targetExtractRootPath, string directory, string filename)
        {
            string filePath = Path.Combine(targetExtractRootPath, directory, filename);
            TryDeleteResource(filePath);
        }

        private static void TryDeleteResource(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
