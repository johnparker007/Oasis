using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Oasis.Download
{
    public static class DownloadUtility
    {
        public static async Task DownloadFileAsync(string url, string destinationPath, Action<long> onDownloadProgress = null)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(destinationPath);
                var operation = request.SendWebRequest();

                long lastReportedBytes = -1;
                while (!operation.isDone)
                {
                    long downloadedBytes = (long)request.downloadedBytes;
                    if (downloadedBytes != lastReportedBytes)
                    {
                        lastReportedBytes = downloadedBytes;
                        onDownloadProgress?.Invoke(downloadedBytes);
                    }

                    await Task.Yield();
                }

                long finalDownloadedBytes = (long)request.downloadedBytes;
                if (finalDownloadedBytes != lastReportedBytes)
                {
                    onDownloadProgress?.Invoke(finalDownloadedBytes);
                }

#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    throw new InvalidOperationException(string.Format("Failed to download file from '{0}': {1}", url, request.error));
                }
            }
        }
    }
}
