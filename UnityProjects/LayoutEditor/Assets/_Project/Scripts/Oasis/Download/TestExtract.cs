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

            System.Threading.Tasks.Task<string> task = MameDownloader.Instance.DownloadAndExtractAsync();
        }
    }
}
