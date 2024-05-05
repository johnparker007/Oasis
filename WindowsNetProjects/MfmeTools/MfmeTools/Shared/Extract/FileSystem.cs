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
        public static readonly string kReelsDirectoryName = "reels";
        public static readonly string kLampsDirectoryName = "lamps";
        public static readonly string kButtonsDirectoryName = "buttons";
        public static readonly string kBitmapsDirectoryName = "bitmaps";


        public static void CreateDirectories(string targetExtractRootPath)
        {
            if (!Directory.Exists(targetExtractRootPath))
            {
                Directory.CreateDirectory(targetExtractRootPath);
            }

            CreateImageDirectory(targetExtractRootPath, kReelsDirectoryName);
            CreateImageDirectory(targetExtractRootPath, kLampsDirectoryName);
            CreateImageDirectory(targetExtractRootPath, kButtonsDirectoryName);
            CreateImageDirectory(targetExtractRootPath, kBitmapsDirectoryName);
        }

        public static void SaveLayout(Layout layout, string sourceLayoutPath)
        {
            string targetExtractRootPath = GetTargetExtractPathRoot(sourceLayoutPath);

            CreateDirectories(targetExtractRootPath);

            string json = JsonConvert.SerializeObject(layout, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto //, ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            string filePath = Path.Combine(targetExtractRootPath, layout.ASName + ".json");

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


    }
}
