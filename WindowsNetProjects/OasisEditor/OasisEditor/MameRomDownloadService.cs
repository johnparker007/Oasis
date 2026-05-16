using System.IO;
using System.Net.Http;

namespace OasisEditor;

public sealed class MameRomDownloadService
{
    public const string DefaultDownloadRootUrl = "https://archive.org/download/MAME215RomsOnlyMerged/";
    public const string DefaultArchiveExtension = ".zip";
    private static readonly HttpClient HttpClient = new();

    public string DownloadRootUrl { get; set; } = DefaultDownloadRootUrl;
    public string ArchiveExtension { get; set; } = DefaultArchiveExtension;
    public string LocalRomSourceDirectory { get; set; } = string.Empty;
    public string LocalRomArchiveExtension { get; set; } = DefaultArchiveExtension;

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

        var extension = NormalizeArchiveExtension(ArchiveExtension);
        return $"{romName.Trim()}{extension}";
    }

    public string BuildDownloadUrl(string romName)
    {
        var rootUrl = NormalizeDownloadRootUrl(DownloadRootUrl);
        return $"{rootUrl}{BuildRomArchiveFileName(romName)}";
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
        var localSourcePath = GetLocalRomArchivePath(romName);
        if (!string.IsNullOrWhiteSpace(localSourcePath) && File.Exists(localSourcePath))
        {
            File.Copy(localSourcePath, archivePath, overwrite: true);
            return archivePath;
        }

        var downloadUrl = BuildDownloadUrl(romName);
        await using var sourceStream = await HttpClient.GetStreamAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
        await using var targetStream = File.Create(archivePath);
        await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
        return archivePath;
    }

    public string GetLocalRomArchivePath(string romName)
    {
        if (string.IsNullOrWhiteSpace(LocalRomSourceDirectory))
        {
            return string.Empty;
        }

        var directory = LocalRomSourceDirectory.Trim();
        var extension = NormalizeArchiveExtension(LocalRomArchiveExtension);
        var fileName = $"{romName.Trim()}{extension}";
        return Path.Combine(directory, fileName);
    }

    private static string NormalizeDownloadRootUrl(string rootUrl)
    {
        if (string.IsNullOrWhiteSpace(rootUrl)
            || !Uri.TryCreate(rootUrl, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("ROM download base URL must be an absolute HTTP/HTTPS URL.", nameof(rootUrl));
        }

        return rootUrl.EndsWith("/", StringComparison.Ordinal) ? rootUrl : rootUrl + "/";
    }

    private static string NormalizeArchiveExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("ROM archive extension must be provided.", nameof(extension));
        }

        var normalized = extension.Trim().ToLowerInvariant();
        if (!normalized.StartsWith(".", StringComparison.Ordinal))
        {
            normalized = "." + normalized;
        }

        return normalized is ".zip" or ".7z"
            ? normalized
            : throw new ArgumentException("ROM archive extension must be .zip or .7z.", nameof(extension));
    }
}
