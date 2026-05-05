using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;

namespace OasisEditor;

public sealed class MameDownloadService
{
    private static readonly HttpClient HttpClient = new();

    public Task<IReadOnlyList<string>> GetKnownVersionsAsync(CancellationToken cancellationToken)
    {
        // Phase C seed list aligned with legacy default and current branch conventions.
        IReadOnlyList<string> versions = ["0258", "0267", "0281"];
        return Task.FromResult(versions);
    }

    public string GetArchiveUrl(string releaseSource, string version)
    {
        var normalizedVersion = NormalizeVersion(version);
        var archiveName = BuildArchiveFileName(normalizedVersion);
        return $"{releaseSource.TrimEnd('/')}/download/mame{normalizedVersion}/{archiveName}";
    }

    public string GetInstallDirectory(string installRootDirectory, string version)
        => Path.Combine(installRootDirectory, $"mame{NormalizeVersion(version)}");

    public async Task<string> DownloadAndExtractAsync(
        string releaseSource,
        string version,
        string installRootDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedVersion = NormalizeVersion(version);
        var installDirectory = GetInstallDirectory(installRootDirectory, normalizedVersion);
        var archiveName = BuildArchiveFileName(normalizedVersion);
        var archiveUrl = GetArchiveUrl(releaseSource, normalizedVersion);
        var downloadsDirectory = Path.Combine(installRootDirectory, "downloads");
        Directory.CreateDirectory(downloadsDirectory);
        Directory.CreateDirectory(installRootDirectory);

        var archivePath = Path.Combine(downloadsDirectory, archiveName);
        progress?.Report($"Downloading {archiveUrl}");
        await using (var sourceStream = await HttpClient.GetStreamAsync(archiveUrl, cancellationToken))
        await using (var targetStream = File.Create(archivePath))
        {
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
        }

        progress?.Report($"Extracting {archiveName} to {installDirectory}");
        if (Directory.Exists(installDirectory))
        {
            Directory.Delete(installDirectory, recursive: true);
        }

        Directory.CreateDirectory(installDirectory);
        await ExtractArchiveAsync(archivePath, installDirectory, cancellationToken).ConfigureAwait(false);
        return Path.Combine(installDirectory, "mame.exe");
    }

    public bool RemoveCachedVersion(string installRootDirectory, string version)
    {
        var installDirectory = GetInstallDirectory(installRootDirectory, version);
        if (!Directory.Exists(installDirectory))
        {
            return false;
        }

        Directory.Delete(installDirectory, recursive: true);
        return true;
    }

    private static string BuildArchiveFileName(string normalizedVersion)
    {
        var numericVersion = int.Parse(normalizedVersion);
        var suffix = numericVersion >= 281 ? "x64" : "64bit";
        return $"mame{normalizedVersion}b_{suffix}.exe";
    }

    private static async Task ExtractArchiveAsync(string archivePath, string installDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = archivePath,
            Arguments = $"-y -o\"{installDirectory}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = installDirectory
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start MAME extractor executable.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"MAME extraction failed with code {process.ExitCode}: {error}");
        }
    }

    private static string NormalizeVersion(string version)
    {
        var numericOnly = new string(version.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(numericOnly))
        {
            throw new InvalidOperationException("MAME version must contain digits.");
        }

        var numericVersion = int.Parse(numericOnly);
        return numericVersion.ToString("0000");
    }
}
