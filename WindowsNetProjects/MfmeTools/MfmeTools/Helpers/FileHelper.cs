using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Oasis.MfmeTools.Helpers
{
    public static class FileHelper
    {
        public static readonly string kReversedFilenameSuffix = "_REVERSED";

        public static void UseDefaultExtAsFilterIndex(FileDialog dialog)
        {
            var ext = "*." + dialog.DefaultExt;
            var filter = dialog.Filter;
            var filters = filter.Split('|');
            for (int i = 1; i < filters.Length; i += 2)
            {
                if (filters[i] == ext)
                {
                    dialog.FilterIndex = 1 + (i - 1) / 2;
                    return;
                }
            }
        }

        public static void DeleteFileIfFound(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public static void DeleteAllFilesAtAbsolutePath(string absolutePath, bool recreateEmptyDirectory, bool recursive = true)
        {
            absolutePath = absolutePath.Replace("/", "\\");

            try
            {
                Directory.Delete(absolutePath, recursive);
            }
            catch (Exception exception)
            {
            }

            if (recreateEmptyDirectory)
            {
                Directory.CreateDirectory(absolutePath);
            }
        }

        public static void ForceDeleteDirectory(string path, bool recreateEmptyDirectory)
        {
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }

            directory.Delete(true);

            if (recreateEmptyDirectory)
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        public static void CopyTopLevelFiles(string sourcePath, string targetPath)
        {
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath));
            }
        }

        public static void CopyReversedFile(string sourceFilePath, string targetFilePath, bool addReversedSuffix)
        {
            byte[] fileBytes = File.ReadAllBytes(sourceFilePath);
            byte[] fileBytesReversed = fileBytes.Reverse().ToArray();

            if (addReversedSuffix)
            {
                targetFilePath += kReversedFilenameSuffix;
            }

            File.WriteAllBytes(targetFilePath, fileBytesReversed);
        }

        public static void RecursiveSetWritable(string baseDirectoryPath)
        {
            RecursiveSetWritable(new DirectoryInfo(baseDirectoryPath));
        }

        public static void RecursiveSetWritable(DirectoryInfo baseDirectoryInfo)
        {
            if (!baseDirectoryInfo.Exists)
            {
                return;
            }

            foreach (DirectoryInfo subDirectoryInfo in baseDirectoryInfo.EnumerateDirectories())
            {
                RecursiveSetWritable(subDirectoryInfo);
            }

            var files = baseDirectoryInfo.GetFiles();
            foreach (var file in files)
            {
                file.IsReadOnly = false;
            }
        }

        // not yet tested
        public static bool DoesDirectoryContainNamedFile(string directoryPath, string filename)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
            List<FileInfo> fileInfos = directoryInfo.GetFiles().ToList();
            foreach (FileInfo fileInfo in fileInfos)
            {
                if (fileInfo.Name == filename)
                {
                    return true;
                }
            }

            return false;
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, bool overwrite = true)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);

                if (overwrite && File.Exists(targetFilePath))
                {
                    File.SetAttributes(targetFilePath, File.GetAttributes(targetFilePath) & ~FileAttributes.ReadOnly);
                }

                file.CopyTo(targetFilePath, overwrite);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true, overwrite);
                }
            }
        }

    }
}
