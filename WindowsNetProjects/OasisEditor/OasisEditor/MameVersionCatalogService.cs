using System.IO;
using System.Text.Json;

namespace OasisEditor;

public sealed class MameVersionCatalogService : IMameVersionCatalogService
{
    private readonly MameDownloadService _downloadService;
    private readonly string _catalogCachePath;

    public MameVersionCatalogService(MameDownloadService downloadService)
    {
        _downloadService = downloadService;
        var cacheDirectory = Path.Combine(MameRuntimePaths.EnsureManagedRuntimeRootDirectory(), "state");
        Directory.CreateDirectory(cacheDirectory);
        _catalogCachePath = Path.Combine(cacheDirectory, "mame-version-catalog.json");
    }

    public async Task<MameVersionCatalogResult> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var versions = await _downloadService.GetKnownVersionsAsync(cancellationToken).ConfigureAwait(false);
            var ordered = versions.OrderByDescending(v => v, StringComparer.Ordinal).ToArray();
            var latest = ordered.FirstOrDefault();
            await SaveCacheAsync(new CatalogCacheModel(ordered, latest), cancellationToken).ConfigureAwait(false);
            return new MameVersionCatalogResult(latest, ordered, false);
        }
        catch
        {
            var cached = await TryLoadCacheAsync(cancellationToken).ConfigureAwait(false);
            if (cached is null)
            {
                return new MameVersionCatalogResult(null, Array.Empty<string>(), true);
            }

            return new MameVersionCatalogResult(cached.LatestVersion, cached.KnownVersions ?? Array.Empty<string>(), true);
        }
    }

    public async Task<IReadOnlyList<string>> GetKnownVersionsAsync(CancellationToken cancellationToken)
    {
        var result = await GetLatestVersionAsync(cancellationToken).ConfigureAwait(false);
        return result.KnownVersions;
    }

    private async Task SaveCacheAsync(CatalogCacheModel model, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_catalogCachePath, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CatalogCacheModel?> TryLoadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_catalogCachePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_catalogCachePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<CatalogCacheModel>(json);
        }
        catch
        {
            return null;
        }
    }

    private sealed record CatalogCacheModel(IReadOnlyList<string> KnownVersions, string? LatestVersion);
}
