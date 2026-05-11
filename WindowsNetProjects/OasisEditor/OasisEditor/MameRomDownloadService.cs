using System.Net.Http;
using System.IO;

namespace OasisEditor;

public sealed class MameRomDownloadService
{
    private const string DownloadRootUrl = "https://archive.org/download/CentralArquivistaArcade/";
    private static readonly HttpClient HttpClient = new();

    public static string GetRomDownloadDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "OasisEditor", "MAME", "roms");
    }

    public string BuildRomArchiveFileName(string romName)
    {
        if (string.IsNullOrWhiteSpace(romName))
        {
            throw new ArgumentException("ROM name must be provided.", nameof(romName));
        }

        return $"{romName.Trim()}.zip";
    }

    public string BuildDownloadUrl(string romName)
    {
        return $"{DownloadRootUrl}{BuildRomArchiveFileName(romName)}";
    }

    public string GetRomArchivePath(string romName)
    {
        return Path.Combine(GetRomDownloadDirectory(), BuildRomArchiveFileName(romName));
    }

    public bool IsRomInstalled(string romName)
    {
        if (string.IsNullOrWhiteSpace(romName))
        {
            return false;
        }

        return File.Exists(GetRomArchivePath(romName));
    }

    public async Task<string> DownloadRomAsync(string romName, CancellationToken cancellationToken)
    {
        var archivePath = GetRomArchivePath(romName);
        if (File.Exists(archivePath))
        {
            return archivePath;
        }

        Directory.CreateDirectory(GetRomDownloadDirectory());
        var downloadUrl = BuildDownloadUrl(romName);
        await using var sourceStream = await HttpClient.GetStreamAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
        await using var targetStream = File.Create(archivePath);
        await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
        return archivePath;
    }
}
