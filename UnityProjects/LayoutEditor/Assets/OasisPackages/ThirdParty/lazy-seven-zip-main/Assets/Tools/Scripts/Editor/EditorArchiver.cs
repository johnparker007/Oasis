#if UNITY_EDITOR
using System.Linq;
using System.Threading.Tasks;
using SevenZip;
using UnityEditor;
using UnityEngine;

namespace LazyJedi.SevenZip
{
    public static class EditorArchiver
    {
        #region METHODS

        [MenuItem("Assets/Archive/Quick/Zip", false, 19)]
        public static async void ArchiveToZip()
        {
            await ArchiveHelper(OutArchiveFormat.Zip, "zip");
        }

        [MenuItem("Assets/Archive/Quick/7z", false, 19)]
        public static async void ArchiveTo7Z()
        {
            await ArchiveHelper(OutArchiveFormat.SevenZip, "7z");
        }

        [MenuItem("Assets/Archive/Quick/Gz", false, 19)]
        public static async void ArchiveToGz()
        {
            await ArchiveHelper(OutArchiveFormat.GZip, "gz");
        }

        [MenuItem("Assets/Archive/Quick/Tar", false, 19)]
        public static async void ArchiveToTar()
        {
            await ArchiveHelper(OutArchiveFormat.Tar, "tar");
        }

        #endregion

        #region HELPER METHODS

        private static async Task ArchiveHelper(OutArchiveFormat format, string extension)
        {
            string[] inFiles = Selection.objects.Select(AssetDatabase.GetAssetPath).ToArray();
            string   outPath = EditorUtility.SaveFilePanel("Save", Application.dataPath, "", extension);
            await LazyArchiver.ArchiveAsync(outPath, inFiles, format);
            AssetDatabase.Refresh();
        }

        #endregion
    }
}
#endif