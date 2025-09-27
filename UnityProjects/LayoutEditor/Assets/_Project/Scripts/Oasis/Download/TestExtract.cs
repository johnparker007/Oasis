using NativeWindowsUI;
using UnityEngine;

namespace Oasis.Download
{
    public class TestExtract : MonoBehaviour
    {
        //public string ArchivePath;
        //public string TargetPath;

        protected void Start()
        {
            //LazyExtractor.Extract(TargetPath, ArchivePath);
            BeginTest();


        }

        private async void BeginTest()
        {
            NativeProgressDialog.ShowDialog("test title", "test message", null, false);
            await MameDownloader.Instance.DownloadAndExtractAsync();
            NativeProgressDialog.HideDialog();
        }
    }
}
