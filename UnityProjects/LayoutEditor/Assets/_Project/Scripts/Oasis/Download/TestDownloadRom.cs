using UnityEngine;

namespace Oasis.Download
{
    public class TestDownloadRom : MonoBehaviour
    {
        //public string ArchivePath;
        //public string TargetPath;

        public string MameRomName;

        protected void Start()
        {
            //LazyExtractor.Extract(TargetPath, ArchivePath);

            System.Threading.Tasks.Task<string> task = MameRomDownloader.Instance.DownloadRomAsync(MameRomName);
        }
    }
}
