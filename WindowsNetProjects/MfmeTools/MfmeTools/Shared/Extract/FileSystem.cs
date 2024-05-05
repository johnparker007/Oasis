using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static bool UseCachedBitmapImages
        {
            get;
            private set;
        }


        public static void Setup(string sourceLayoutPath, 
            bool useCachedBackgroundImage,
            bool useCachedReelImages,
            bool useCachedLampImages,
            bool useCachedBitmapImages)
        {
            UseCachedBackgroundImage = useCachedBackgroundImage;
            UseCachedReelImages = useCachedReelImages;
            UseCachedLampImages = useCachedLampImages;
            UseCachedBitmapImages = useCachedBitmapImages;

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

            CreateImageDirectory(targetExtractRootPath, kBackgroundDirectoryName);
            CreateImageDirectory(targetExtractRootPath, kReelsDirectoryName);
            CreateImageDirectory(targetExtractRootPath, kLampsDirectoryName);
            CreateImageDirectory(targetExtractRootPath, kButtonsDirectoryName);
            CreateImageDirectory(targetExtractRootPath, kBitmapsDirectoryName);
        }

        public static void TryDeleteBackgroundImage(string filename)
        {
            TryDeleteImage(_targetExtractRootPath, kBackgroundDirectoryName, filename);
        }

        public static string GetFullBackgroundImagePath(string filename)
        {
            return Path.Combine(_targetExtractRootPath, kBackgroundDirectoryName, filename);
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

        private static void CreateImageDirectory(string targetExtractRootPath, string imageDirectoryName)
        {
            string imageDirectoryPath = Path.Combine(targetExtractRootPath, imageDirectoryName);
            if (!Directory.Exists(imageDirectoryPath))
            {
                Directory.CreateDirectory(imageDirectoryPath);
            }
        }

        private static void TryDeleteImage(string targetExtractRootPath, string directory, string filename)
        {
            string filePath = Path.Combine(targetExtractRootPath, directory, filename);

            if(File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
