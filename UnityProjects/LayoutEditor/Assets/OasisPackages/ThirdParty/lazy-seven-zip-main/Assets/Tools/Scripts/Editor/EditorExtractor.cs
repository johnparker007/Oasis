#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LazyJedi.SevenZip
{
    public static class EditorExtractor
    {
        #region METHODS

        [MenuItem("Assets/Archive/Extract/Files...", false, 19)]
        public static async void ExtractFiles()
        {
            await ExtractArchiveHelper(inArchive =>
            {
                string outPath = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, "");
                return Path.GetFullPath(outPath);
            });
        }

        [MenuItem("Assets/Archive/Extract/Here", false, 19)]
        public static async void ExtractHere()
        {
            await ExtractArchiveHelper(Path.GetDirectoryName);
        }

        [MenuItem("Assets/Archive/Extract/To Folder", false, 19)]
        public static async void ExtractToFolder()
        {
            await ExtractArchiveHelper(inArchive =>
            {
                string outPath = Path.Combine(Path.GetDirectoryName(inArchive), Path.GetFileNameWithoutExtension(inArchive));
                if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);
                return outPath;
            });
        }

        #endregion

        #region HELPER METHODS

        private static async Task ExtractArchiveHelper(Func<string, string> outPathCallback)
        {
            Object[] selectedObjects = Selection.objects;
            foreach (Object selectedObject in selectedObjects)
            {
                string inArchive = AssetDatabase.GetAssetPath(selectedObject);
                if (!IsArchive(inArchive)) continue;

                await LazyExtractor.ExtractAsync(outPathCallback?.Invoke(inArchive), inArchive);
                AssetDatabase.Refresh();
            }
        }

        private static bool IsArchive(string inArchive)
        {
            if ( LazySevenZipHelper.IsArchiveExtension(Path.GetExtension(inArchive))) return true;
            Debug.LogError($"Cannot extract from the following file - {Path.GetFileName(inArchive)}");
            return false;
        }

        #endregion
    }
}
#endif