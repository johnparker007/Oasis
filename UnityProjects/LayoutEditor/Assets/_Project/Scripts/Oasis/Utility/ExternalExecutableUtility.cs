using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Oasis.Utility
{
    public static class ExternalExecutableUtility
    {
        public const string ExternalAssetsRootFolderName = "LayoutEditor_ExternalAssets";
        public const string WindowsExternalSubFolderName = "Windows";
        public const string BuildExecutableDirectoryName = "ExternalExecutables";

        public static Process RunExe(string executableFileName, string arguments = "", bool createNoWindow = true)
        {
            if (!IsWindowsPlatform())
            {
                Debug.LogError("External executables can only be launched when running on Windows.");
                return null;
            }

            string executablePath = GetExecutablePath(executableFileName);
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            {
                Debug.LogError($"External executable '{executableFileName}' was not found at path '{executablePath}'.");
                return null;
            }

            var startInfo = new ProcessStartInfo(executablePath)
            {
                Arguments = arguments ?? string.Empty,
                CreateNoWindow = createNoWindow,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty
            };

            try
            {
                return Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to launch external executable '{executablePath}'. {exception}");
                return null;
            }
        }

        public static string GetExecutablePath(string executableFileName)
        {
            if (string.IsNullOrEmpty(executableFileName))
            {
                throw new ArgumentException("Executable file name must be provided.", nameof(executableFileName));
            }

            string executableDirectory = GetExecutableDirectory();
            return Path.Combine(executableDirectory, executableFileName);
        }

        public static string GetExternalAssetsDirectory()
        {
            return GetExecutableDirectory();
        }

        public static bool HasEditorExecutables()
        {
#if UNITY_EDITOR
            string directory = GetEditorExecutableDirectory();
            return Directory.Exists(directory) && Directory.GetFiles(directory).Length > 0;
#else
            return false;
#endif
        }

        private static string GetExecutableDirectory()
        {
#if UNITY_EDITOR
            return GetEditorExecutableDirectory();
#else
            return GetPlayerExecutableDirectory();
#endif
        }

        private static string GetPlayerExecutableDirectory()
        {
            string playerRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            return Path.Combine(playerRoot, BuildExecutableDirectoryName);
        }

        private static bool IsWindowsPlatform()
        {
#if UNITY_EDITOR
            return Application.platform == RuntimePlatform.WindowsEditor;
#else
            return Application.platform == RuntimePlatform.WindowsPlayer;
#endif
        }

#if UNITY_EDITOR
        public static string GetEditorExecutableDirectory()
        {
            string projectCollectionDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(projectCollectionDirectory, ExternalAssetsRootFolderName, WindowsExternalSubFolderName);
        }

        public static void CopyExecutablesForBuild(string buildOutputDirectory)
        {
            if (string.IsNullOrEmpty(buildOutputDirectory))
            {
                throw new ArgumentException("Build output directory must be provided.", nameof(buildOutputDirectory));
            }

            string sourceDirectory = GetEditorExecutableDirectory();
            if (!Directory.Exists(sourceDirectory))
            {
                Debug.LogWarning($"External executable source directory '{sourceDirectory}' does not exist. No files will be copied.");
                return;
            }

            string destinationDirectory = Path.Combine(buildOutputDirectory, BuildExecutableDirectoryName);
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory))
            {
                string fileName = Path.GetFileName(sourceFilePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                string destinationFilePath = Path.Combine(destinationDirectory, fileName);
                File.Copy(sourceFilePath, destinationFilePath, true);
            }

            foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
            {
                string directoryName = Path.GetFileName(sourceSubDirectory);
                if (string.IsNullOrEmpty(directoryName))
                {
                    continue;
                }

                string destinationSubDirectory = Path.Combine(destinationDirectory, directoryName);
                CopyDirectoryRecursive(sourceSubDirectory, destinationSubDirectory);
            }

            Debug.Log($"Copied external executables to '{destinationDirectory}'.");
        }

        private static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string filePath in Directory.GetFiles(sourceDirectory))
            {
                string fileName = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                string destinationFilePath = Path.Combine(destinationDirectory, fileName);
                File.Copy(filePath, destinationFilePath, true);
            }

            foreach (string subDirectory in Directory.GetDirectories(sourceDirectory))
            {
                string directoryName = Path.GetFileName(subDirectory);
                if (string.IsNullOrEmpty(directoryName))
                {
                    continue;
                }

                string destinationSubDirectory = Path.Combine(destinationDirectory, directoryName);
                CopyDirectoryRecursive(subDirectory, destinationSubDirectory);
            }
        }
#endif
    }
}
