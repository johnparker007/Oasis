using System;
using SevenZip;
using UnityEngine;

namespace LazyJedi.SevenZip
{
    public enum ActionType
    {
        Archive,
        Extract,
        ArchivePassword,
        ExtractPassword
    }

    public class Example_LazySevenZip : MonoBehaviour
    {
        #region FIELDS

        public bool Button;
        public string ArchivePath = @"Assets/files.7z";
        public string Password = "1234";
        public ActionType ActionType = ActionType.Archive;
        public string[] FilesAndFolders;

        #endregion

        #region UNITY METHODS

        private async void OnValidate()
        {
            if (Button)
            {
                Button = false;
                switch (ActionType)
                {
                    case ActionType.Archive:
                        await LazyArchiver.ArchiveAsync(ArchivePath, FilesAndFolders, OutArchiveFormat.SevenZip);
                        break;
                    case ActionType.Extract:
                        await LazyExtractor.ExtractAsync("", ArchivePath);
                        break;
                    case ActionType.ArchivePassword:
                        await LazyArchiver.ArchiveAsync(ArchivePath, FilesAndFolders, Password, OutArchiveFormat.SevenZip);
                        break;
                    case ActionType.ExtractPassword:
                        await LazyExtractor.ExtractAsync("", ArchivePath, Password);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
        }

        #endregion
    }
}